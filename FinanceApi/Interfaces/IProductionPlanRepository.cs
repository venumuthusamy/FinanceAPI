using FinanceApi.ModelDTO;

namespace FinanceApi.Interfaces
{
    public interface IProductionPlanRepository
    {
        Task<IEnumerable<SoHeaderDto>> GetSalesOrdersAsync();
        Task<ProductionPlanResponseDto> GetBySalesOrderAsync(int salesOrderId, int warehouseId);
        Task<int> SavePlanAsync(SavePlanRequest req);
        Task<List<ProductionPlanListDto>> ListPlansWithLinesAsync();
    }
}
