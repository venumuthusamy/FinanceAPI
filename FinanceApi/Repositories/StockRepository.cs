using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace FinanceApi.Repositories
{
    public class StockRepository : DynamicRepository,IStockRepository
    {

        public StockRepository(IDbConnectionFactory connectionFactory)
     : base(connectionFactory)
        {
        }

        public async Task<IEnumerable<StockDTO>> GetAllAsync()
        {
            const string query = @"
                SELECT * from Stock";

            return await Connection.QueryAsync<StockDTO>(query);
        }


        public async Task<StockDTO> GetByIdAsync(long id)
        {

            const string query = "SELECT * FROM Stock WHERE Id = @Id";

            return await Connection.QuerySingleAsync<StockDTO>(query, new { Id = id });
        }

        public async Task<int> InsertBulkAsync(IEnumerable<Stock> stocks)
        {
            const string query = @"
        INSERT INTO Stock (
            ItemID,
            FromWarehouseID,
            ToWarehouseID,
            Available,
            OnHand,
            Reserved,
            Min,
            Expiry,
            isApproved,
            CreatedBy,
            CreatedDate,
            UpdatedBy,
            UpdatedDate
        )
        VALUES (
            @ItemID,
            @FromWarehouseID,
            @ToWarehouseID,
            @Available,
            @OnHand,
            @Reserved,
            @Min,
            @Expiry,
            @isApproved,
            @CreatedBy,
            @CreatedDate,
            @UpdatedBy,
            @UpdatedDate
        );";

            // ExecuteAsync returns total affected rows
            return await Connection.ExecuteAsync(query, stocks);
        }



        public async Task UpdateAsync(Stock stock)
        {
            const string query = "UPDATE Stock SET OnHand = @OnHand,Available =@Available WHERE Id = @Id";
            await Connection.ExecuteAsync(query, stock);
        }

        public async Task DeactivateAsync(int id)
        {
            const string query = "UPDATE Stock SET IsActive = 0 WHERE ID = @id";
            await Connection.ExecuteAsync(query, new { ID = id });
        }
    }
}
