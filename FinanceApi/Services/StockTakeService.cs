using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class StockTakeService : IStockTakeService
    {
        private readonly IStockTakeRepository _repository;

        public StockTakeService(IStockTakeRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<StockTakeDTO>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<IEnumerable<StockTakeWarehouseItem>> GetWarehouseItemsAsync(
                        long warehouseId,long supplierId,byte takeTypeId, long? strategyId)
        {
            // (Optional) defensive check; your controller already validates this.
            if (takeTypeId == 2 && strategyId is null)
                throw new ArgumentException("strategyId is required when takeTypeId = 2 (Cycle).", nameof(strategyId));

            return await _repository.GetWarehouseItemsAsync(warehouseId, supplierId,takeTypeId, strategyId);
        }

        public async Task<StockTakeDTO?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<int> CreateAsync(StockTake stockTake)
        {
            // Example validation logic you might add later
            if (stockTake.LineItems == null || !stockTake.LineItems.Any())
                throw new ArgumentException("At least one line item is required.");

            return await _repository.CreateAsync(stockTake);
        }

        public async Task UpdateAsync(StockTake stockTake)
        {
            await _repository.UpdateAsync(stockTake);
        }

        public async Task DeleteLicense(int id, int updatedBy)
        {
            await _repository.DeactivateAsync(id, updatedBy);
        }

        public Task<int> CreateInventoryAdjustmentsFromStockTakeAsync(
            int stockTakeId, string? reason, string? remarks, string userName,
            bool applyToStock = true, bool markPosted = true, DateTime? txnDateOverride = null, bool onlySelected = true)
        {
            return _repository.CreateFromStockTakeAsync(
                stockTakeId, reason, remarks, userName,
                applyToStock, markPosted, txnDateOverride, onlySelected);
        }
    }
}
