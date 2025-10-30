using System.Data;
using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.AspNetCore.Connections;
using Microsoft.Data.SqlClient;
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
                      -- Get PRs with ONLY their remaining (unused) lines and also in potemp tables checked
-- Match key: PRNo + ItemCode + ItemName (case-insensitive, trimmed)
;WITH UsedLines AS (
    /* Lines used by FINAL POs */
    SELECT
        JSON_VALUE(j.value, '$.prNo') AS prNo,
        LTRIM(RTRIM(
            CASE WHEN CHARINDEX(' - ', JSON_VALUE(j.value,'$.item')) > 0
                 THEN LEFT(JSON_VALUE(j.value,'$.item'), CHARINDEX(' - ', JSON_VALUE(j.value,'$.item')) - 1)
                 ELSE JSON_VALUE(j.value,'$.item')
            END
        )) AS itemCode,
        LTRIM(RTRIM(
            CASE WHEN CHARINDEX(' - ', JSON_VALUE(j.value,'$.item')) > 0
                 THEN SUBSTRING(JSON_VALUE(j.value,'$.item'),
                                CHARINDEX(' - ', JSON_VALUE(j.value,'$.item')) + 3, 4000)
                 ELSE ''
            END
        )) AS itemName
    FROM PurchaseOrder po
    CROSS APPLY OPENJSON(po.PoLines) AS j

    UNION ALL

    /* Lines used by ACTIVE DRAFTs */
    SELECT
        JSON_VALUE(j.value, '$.prNo') AS prNo,
        LTRIM(RTRIM(
            CASE WHEN CHARINDEX(' - ', JSON_VALUE(j.value,'$.item')) > 0
                 THEN LEFT(JSON_VALUE(j.value,'$.item'), CHARINDEX(' - ', JSON_VALUE(j.value,'$.item')) - 1)
                 ELSE JSON_VALUE(j.value,'$.item')
            END
        )) AS itemCode,
        LTRIM(RTRIM(
            CASE WHEN CHARINDEX(' - ', JSON_VALUE(j.value,'$.item')) > 0
                 THEN SUBSTRING(JSON_VALUE(j.value,'$.item'),
                                CHARINDEX(' - ', JSON_VALUE(j.value,'$.item')) + 3, 4000)
                 ELSE ''
            END
        )) AS itemName
    FROM PurchaseOrderTemp pot
    CROSS APPLY OPENJSON(pot.PoLines) AS j
    WHERE ISNULL(pot.IsActive,1) = 1
          -- OR: pot.Status IN ('Draft','Saved')
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

    /* Only UNUSED lines per PR */
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
          ON UPPER(LTRIM(RTRIM(u.prNo)))     = UPPER(LTRIM(RTRIM(pr.PurchaseRequestNo)))
         AND UPPER(LTRIM(RTRIM(u.itemCode))) = UPPER(LTRIM(RTRIM(ISNULL(prj.itemCode, ''))))
         AND UPPER(LTRIM(RTRIM(u.itemName))) = UPPER(LTRIM(RTRIM(ISNULL(prj.itemSearch, ''))))
        WHERE u.prNo IS NULL
        FOR JSON PATH
    ) AS prLines
FROM PurchaseRequest pr
LEFT JOIN Department d ON d.Id = pr.DepartmentID
WHERE pr.IsActive = 1
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

        public async Task<List<CreatedPrDto>> CreateFromReorderSuggestionsAsync(
       List<ReorderSuggestionGroupDto> groups,
       string requester,
       long requesterId,
       long? deptId,
       string? note)
        {
            // ✅ Get a brand-new connection for this method call
            var conn = Connection;
            if (conn.State != ConnectionState.Open)
                await (conn as SqlConnection)!.OpenAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                // 1) Pull item meta once (guard empty IN)
                var allItemIds = groups.SelectMany(g => g.Lines)
                                       .Select(l => l.ItemId)
                                       .Distinct()
                                       .ToArray();

                var itemInfo = allItemIds.Length == 0
     ? new Dictionary<long, ItemMeta>()
     : (await conn.QueryAsync<ItemMeta>(@"
SELECT 
    im.Id                                  AS ItemId,
    im.Sku                                 AS Sku,
    im.Name                                AS Name,
    -- prefer Item.UomId -> Uom.Name/Code, else fall back to ItemMaster.Uom text
    COALESCE(u.Name, im.Uom, '')   AS Uom,
    i.BudgetLineId               AS Budget
   
FROM dbo.ItemMaster im
LEFT JOIN dbo.Item i            ON i.ItemCode = im.Sku      -- or ON i.ItemId = im.Id if you have that FK
LEFT JOIN dbo.Uom  u            ON u.Id = i.UomId
WHERE im.Id IN @Ids;",
     new { Ids = allItemIds }, tx
 )).ToDictionary(x => x.ItemId, x => x);

                var created = new List<CreatedPrDto>();

                foreach (var g in groups)
                {
                    var lines = g.Lines
                        .Where(l => l.Qty > 0)
                        .Select(l =>
                        {
                            itemInfo.TryGetValue(l.ItemId, out var info);
                            return new
                            {
                                itemId = l.ItemId,
                                itemCode = info?.Sku,
                                itemName = info?.Name,
                                qty = l.Qty,
                                price = l.Price,
                                uom = info?.Uom ?? "",
                                budget = info?.Budget ?? 0m,
                                supplierId = g.SupplierId,
                                warehouseId = g.WarehouseId
                            };
                        })
                        .ToList();

                    if (lines.Count == 0) continue;

                    var json = System.Text.Json.JsonSerializer.Serialize(
                        lines,
                        new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                        });

                    // 2) PR number inside same tx & conn
                    var prNo = await NextPrNumberAsync(conn, tx);

                    // 3) Insert PR
                    var prId = await conn.QueryFirstAsync<int>(@"
INSERT INTO PurchaseRequest
(Requester, DepartmentID, DeliveryDate, MultiLoc, Oversea, PRLines,
 CreatedDate, UpdatedDate, CreatedBy, UpdatedBy, Description, PurchaseRequestNo, IsActive, Status,IsReorder)
OUTPUT INSERTED.ID
VALUES
(@Requester, @DepartmentID, @DeliveryDate, 0, 0, @PRLines,
 @UtcNow, @UtcNow, @CreatedBy, @UpdatedBy, @Description, @PRNo, 1, 1,@IsReorder);",
                        new
                        {
                            Requester = requester,
                            DepartmentID = (object?)deptId ?? DBNull.Value,
                            DeliveryDate = DateTime.UtcNow.Date.AddDays(1),
                            PRLines = json,
                            UtcNow = DateTime.UtcNow,
                            CreatedBy = requesterId.ToString(),
                            UpdatedBy = requesterId.ToString(),
                            Description = string.IsNullOrWhiteSpace(note)
                                           ? "Auto-created from Reorder Planning"
                                           : note,
                            PRNo = prNo,
                            IsReorder = 1
                        }, tx);

                    created.Add(new CreatedPrDto { Id = prId, PurchaseRequestNo = prNo });
                }

                tx.Commit();
                return created;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // ⚠️ Helper takes the SAME conn + tx
        private static async Task<string> NextPrNumberAsync(IDbConnection conn, IDbTransaction tx)
        {
            const string sql = @"
        SELECT TOP 1 PurchaseRequestNo
          FROM PurchaseRequest
         WHERE ISNUMERIC(SUBSTRING(PurchaseRequestNo, 4, LEN(PurchaseRequestNo))) = 1
         ORDER BY Id DESC;";

            var last = await conn.QueryFirstOrDefaultAsync<string>(sql, transaction: tx);
            var next = 1;
            if (!string.IsNullOrWhiteSpace(last) && last.StartsWith("PR-"))
            {
                var part = last[3..];
                if (int.TryParse(part, out var n)) next = n + 1;
            }
            return $"PR-{next:D4}";
        }

    }
}
