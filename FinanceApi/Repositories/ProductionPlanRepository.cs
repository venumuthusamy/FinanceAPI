using System.Data;
using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;

namespace FinanceApi.Repositories
{
    public class ProductionPlanRepository : DynamicRepository, IProductionPlanRepository
    {
        private readonly IDbConnection _db;
        public ProductionPlanRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

        public async Task<IEnumerable<SoHeaderDto>> GetSalesOrdersAsync()
        {
            const string sql = @"
SELECT TOP 200
  Id,
  SalesOrderNo,
  CustomerId,
  DeliveryDate,
  Status
FROM dbo.SalesOrder
WHERE ISNULL(IsActive,1)=1
ORDER BY Id DESC;";
            return await Connection.QueryAsync<SoHeaderDto>(sql);
        }

        public async Task<ProductionPlanResponseDto> GetBySalesOrderAsync(int salesOrderId, int warehouseId)
        {
            using var multi = await Connection.QueryMultipleAsync(
                "dbo.sp_PP_GetBySalesOrder",
                new { SalesOrderId = salesOrderId, WarehouseId = warehouseId },
                commandType: CommandType.StoredProcedure);

            var plan = (await multi.ReadAsync<PlanRowDto>()).ToList();
            var ing = (await multi.ReadAsync<IngredientRowDto>()).ToList();

            return new ProductionPlanResponseDto { PlanRows = plan, Ingredients = ing };
        }

        public async Task<int> SavePlanAsync(SavePlanRequest req)
        {
            var planId = await Connection.ExecuteScalarAsync<int>(
                "dbo.sp_PP_SavePlan",
                new
                {
                    SalesOrderId = req.SalesOrderId,
                    OutletId = req.OutletId,
                    WarehouseId = req.WarehouseId,
                    CreatedBy = req.CreatedBy
                },
                commandType: CommandType.StoredProcedure);

            return planId;
        }
    }
}
