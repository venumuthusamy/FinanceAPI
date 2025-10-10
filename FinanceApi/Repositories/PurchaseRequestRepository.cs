using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace FinanceApi.Repositories
{
    public class PurchaseRequestRepository : DynamicRepository, IPurchaseRequestRepository
    {
        private readonly ApplicationDbContext _context;

        public PurchaseRequestRepository(IDbConnectionFactory connectionFactory)
          : base(connectionFactory)
        {
        }

        public async Task<IEnumerable<PurchaseRequestDTO>> GetAllAsync()
        {
            const string query = @"
                SELECT p.*,d.DepartmentName
 from PurchaseRequest as p 
 inner join Department as d on
 p.DepartmentID=d.Id  
 where p.IsActive=1";

            return await Connection.QueryAsync<PurchaseRequestDTO>(query);
        }


        public async Task<PurchaseRequestDTO> GetByIdAsync(int id)
        {

            const string query = "SELECT * FROM PurchaseRequest WHERE Id = @Id";

            return await Connection.QuerySingleAsync<PurchaseRequestDTO>(query, new { Id = id });
        }

        public async Task<IEnumerable<PurchaseRequestDTO>> GetAvailablePurchaseRequestsAsync()
        {

            //var sql = @"
            //SELECT pr.*
            //FROM PurchaseRequest pr
            //WHERE pr.IsActive = 1
            //  AND pr.PurchaseRequestNo NOT IN (
            //    SELECT DISTINCT JSON_VALUE(j.value, '$.prNo')
            //    FROM PurchaseOrder po
            //    CROSS APPLY OPENJSON(po.PoLines) AS j
            //  )
            //ORDER BY pr.Id DESC;";

            var sql = @"
                      -- Get PRs with ONLY their remaining (unused) lines
-- Match key: PRNo + ItemCode + ItemName (case-insensitive, trimmed)
WITH UsedLines AS (
    SELECT
        JSON_VALUE(j.value, '$.prNo') AS prNo,
        -- Split ""CODE - NAME"" safely (handles missing delimiter)
        LTRIM(RTRIM(
            CASE 
                WHEN CHARINDEX(' - ', JSON_VALUE(j.value,'$.item')) > 0
                    THEN LEFT(JSON_VALUE(j.value,'$.item'), CHARINDEX(' - ', JSON_VALUE(j.value,'$.item')) - 1)
                ELSE JSON_VALUE(j.value,'$.item')
            END
        )) AS itemCode,
        LTRIM(RTRIM(
            CASE 
                WHEN CHARINDEX(' - ', JSON_VALUE(j.value,'$.item')) > 0
                    THEN SUBSTRING(
                            JSON_VALUE(j.value,'$.item'),
                            CHARINDEX(' - ', JSON_VALUE(j.value,'$.item')) + 3,
                            4000
                         )
                ELSE ''
            END
        )) AS itemName
    FROM PurchaseOrder po
    CROSS APPLY OPENJSON(po.PoLines) AS j
)

SELECT
    pr.Id,
    pr.PurchaseRequestNo,
    pr.Requester,
    pr.DepartmentID,
    pr.DeliveryDate,
    pr.Description,
    pr.MultiLoc,
    pr.Oversea,
    pr.CreatedDate,
    pr.UpdatedDate,
    pr.CreatedBy,
    pr.UpdatedBy,
    pr.IsActive,
    pr.Status,
    ISNULL(d.DepartmentName,'') AS DepartmentName,

    -- Rebuild prLines as JSON of ONLY the unused lines
    (
        SELECT
            prj.itemSearch,
            prj.itemCode,
            prj.qty,
            prj.uomSearch,
            prj.uom,
            prj.locationSearch,
            prj.location,
            prj.budget,
            prj.remarks
        FROM OPENJSON(pr.prLines)
        WITH (
            itemSearch      NVARCHAR(200) '$.itemSearch',
            itemCode        NVARCHAR(100) '$.itemCode',
            qty             DECIMAL(18,4) '$.qty',
            uomSearch       NVARCHAR(100) '$.uomSearch',
            uom             NVARCHAR(100) '$.uom',
            locationSearch  NVARCHAR(200) '$.locationSearch',
            location        NVARCHAR(200) '$.location',
            budget          NVARCHAR(400) '$.budget',
            remarks         NVARCHAR(400) '$.remarks'
        ) AS prj
        LEFT JOIN UsedLines u
            ON UPPER(LTRIM(RTRIM(u.prNo)))      = UPPER(LTRIM(RTRIM(pr.PurchaseRequestNo)))
           AND UPPER(LTRIM(RTRIM(u.itemCode)))  = UPPER(LTRIM(RTRIM(ISNULL(prj.itemCode, ''))))
           AND UPPER(LTRIM(RTRIM(u.itemName)))  = UPPER(LTRIM(RTRIM(ISNULL(prj.itemSearch, ''))))
        WHERE u.prNo IS NULL
        FOR JSON PATH
    ) AS prLines
FROM PurchaseRequest pr
LEFT JOIN Department d ON d.Id = pr.DepartmentID
WHERE
    pr.IsActive = 1
    -- Keep only PRs that still have at least one unused line
    AND EXISTS (
        SELECT 1
        FROM OPENJSON(pr.prLines)
        WITH (
            itemSearch NVARCHAR(200) '$.itemSearch',
            itemCode   NVARCHAR(100) '$.itemCode'
        ) AS prj2
        LEFT JOIN UsedLines u2
            ON UPPER(LTRIM(RTRIM(u2.prNo)))     = UPPER(LTRIM(RTRIM(pr.PurchaseRequestNo)))
           AND UPPER(LTRIM(RTRIM(u2.itemCode))) = UPPER(LTRIM(RTRIM(ISNULL(prj2.itemCode, ''))))
           AND UPPER(LTRIM(RTRIM(u2.itemName))) = UPPER(LTRIM(RTRIM(ISNULL(prj2.itemSearch, ''))))
        WHERE u2.prNo IS NULL
    )
ORDER BY pr.Id DESC;

                            ";

            return await Connection.QueryAsync<PurchaseRequestDTO>(sql);

        }


        public async Task<int> CreateAsync(PurchaseRequest pr)
        {
            // Step 1: Get the last PurchaseRequestNo from the database
            const string getLastPurchaseRequestNo = @"SELECT TOP 1 PurchaseRequestNo 
                                               FROM PurchaseRequest 
                                               WHERE ISNUMERIC(SUBSTRING(PurchaseRequestNo, 4, LEN(PurchaseRequestNo))) = 1
                                               ORDER BY Id DESC";

            var lastPR = await Connection.QueryFirstOrDefaultAsync<string>(getLastPurchaseRequestNo);

            int nextNumber = 1;

            // Step 2: Parse the last PR number and increment it
            if (!string.IsNullOrWhiteSpace(lastPR) && lastPR.StartsWith("PR-"))
            {
                var numericPart = lastPR.Substring(3); // Removes "PR-"
                if (int.TryParse(numericPart, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            // Step 3: Format the new PR number
            var newPRNumber = $"PR-{nextNumber.ToString("D4")}";

            // Step 4: Assign to DTO
            pr.PurchaseRequestNo = newPRNumber;
            pr.CreatedDate = DateTime.UtcNow;
            pr.UpdatedDate = DateTime.UtcNow;

            // Step 5: Insert new record
            const string insertQuery = @"
        INSERT INTO PurchaseRequest
            (Requester, DepartmentID, DeliveryDate, MultiLoc, Oversea, PRLines, CreatedDate,
             UpdatedDate, CreatedBy, UpdatedBy, Description, PurchaseRequestNo,IsActive,Status)
        OUTPUT INSERTED.ID
        VALUES
            (@Requester, @DepartmentID, @DeliveryDate, @MultiLoc, @Oversea, @PRLines, @CreatedDate,
             @UpdatedDate, @CreatedBy, @UpdatedBy, @Description, @PurchaseRequestNo,@IsActive,@Status)";

            return await Connection.QueryFirstAsync<int>(insertQuery, pr);
        }


        public async Task UpdateAsync(PurchaseRequest pr)
        {
            const string updateQuery = @"
        UPDATE PurchaseRequest
        SET
            Requester = @Requester,
            DepartmentID = @DepartmentID,
            DeliveryDate = @DeliveryDate,
            MultiLoc = @MultiLoc,
            Oversea = @Oversea,
            PRLines = @PRLines,
            UpdatedDate = @UpdatedDate,
            UpdatedBy = @UpdatedBy,
            Description = @Description
        WHERE ID = @ID";

            pr.UpdatedDate = DateTime.UtcNow;

            await Connection.ExecuteAsync(updateQuery, pr);
        }


        public async Task DeactivateAsync(int id)
        {
            const string query = "UPDATE PurchaseRequest SET IsActive = 0 WHERE ID = @id";
            await Connection.ExecuteAsync(query, new { ID = id });
        }
    }
}
