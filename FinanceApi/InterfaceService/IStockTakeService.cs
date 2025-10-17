using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.InterfaceService
{
    public interface IStockTakeService
    {
        Task<IEnumerable<StockTakeDTO>> GetAllAsync();
        Task<IEnumerable<StockTakeWarehouseItem>> GetWarehouseItemsAsync(long warehouseId, long binId, byte takeTypeId, long? strategyId);
        Task<StockTakeDTO> GetByIdAsync(int id);
        Task<int> CreateAsync(StockTake stockTake);
        Task UpdateAsync(StockTake stockTake);
        Task DeleteLicense(int id, int updatedBy);
    }
}
