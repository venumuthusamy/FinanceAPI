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

        public async Task DeleteLicense(int id)
        {
            await _repository.DeactivateAsync(id);
        }
    }
}
