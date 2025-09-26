using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace FinanceApi.Repositories
{
    public class PurchaseRequestRepository :DynamicRepository,IPurchaseRequestRepository
    {
        private readonly ApplicationDbContext _context;

        public PurchaseRequestRepository(IDbConnectionFactory connectionFactory)
          : base(connectionFactory)
        {
        }

        public async Task<IEnumerable<PurchaseRequestDTO>> GetAllAsync()
        {
            const string query = @"
                SELECT * from PurchaseRequest";

            return await Connection.QueryAsync<PurchaseRequestDTO>(query);
        }


        public async Task<PurchaseRequestDTO> GetByIdAsync(int id)
        {

            const string query = "SELECT * FROM PurchaseRequest WHERE Id = @Id";

            return await Connection.QuerySingleAsync<PurchaseRequestDTO>(query, new { Id = id });
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
             UpdatedDate, CreatedBy, UpdateddBy, Description, PurchaseRequestNo)
        OUTPUT INSERTED.ID
        VALUES
            (@Requester, @DepartmentID, @DeliveryDate, @MultiLoc, @Oversea, @PRLines, @CreatedDate,
             @UpdatedDate, @CreatedBy, @UpdateddBy, @Description, @PurchaseRequestNo)";

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
            UpdateddBy = @UpdateddBy,
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
