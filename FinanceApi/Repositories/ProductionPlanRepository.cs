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
        public async Task<List<ProductionPlanListDto>> ListPlansWithLinesAsync()
        {
            using var multi = await Connection.QueryMultipleAsync(
                "dbo.sp_PP_ListPlans_WithLines",
                commandType: CommandType.StoredProcedure
            );

            var plans = (await multi.ReadAsync<ProductionPlanListDto>()).ToList();
            var lines = (await multi.ReadAsync<ProductionPlanLineDto>()).ToList();

            var dict = plans.ToDictionary(x => x.Id, x => x);
            foreach (var ln in lines)
            {
                if (dict.TryGetValue(ln.ProductionPlanId, out var p))
                    p.Lines.Add(ln);
            }

            return plans;
        }

        public async Task<IEnumerable<ShortageGrnAlertDto>> GetShortageGrnAlertsAsync()
        {
            const string sql = @"
SELECT
    p.Id            AS ProductionPlanId,
    p.SalesOrderId  AS SalesOrderId,

    pg.Id           AS GrnId,
    pg.GrnNo        AS GrnNo,
    pg.ReceptionDate,

    gd.ItemCode     AS ItemCode,
    it.ItemName     AS ItemName,
    CAST(ISNULL(gd.QtyReceived, 0) AS decimal(18,2)) AS QtyReceived
FROM dbo.ProductionPlan p
JOIN dbo.PurchaseGoodReceipt pg
  ON pg.IsActive = 1
 AND pg.SourceType = 'RECIPE_SHORTAGE'
 AND pg.SourceRefId = p.Id              -- ✅ IMPORTANT FIX (use PlanId)
OUTER APPLY OPENJSON(pg.GRNJson)
WITH (
    ItemCode     nvarchar(50)  '$.itemCode',
    QtyReceived  decimal(18,2) '$.qtyReceived'
) AS gd
LEFT JOIN dbo.Item it
  ON it.ItemCode = gd.ItemCode
ORDER BY pg.Id DESC;";

            return await Connection.QueryAsync<ShortageGrnAlertDto>(sql);
        }


    }
}
