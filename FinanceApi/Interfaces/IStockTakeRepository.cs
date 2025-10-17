using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IStockTakeRepository
    {
        Task<IEnumerable<StockTakeDTO>> GetAllAsync();
        Task<IEnumerable<StockTakeWarehouseItem>> GetWarehouseItemsAsync(long warehouseId, long binId, byte takeTypeId, long? strategyId);
        Task<StockTakeDTO> GetByIdAsync(int id);
        Task<int> CreateAsync(StockTake stockTake);
        Task UpdateAsync(StockTake stockTake);
        Task DeactivateAsync(int id, int updatedBy);
    }
}
