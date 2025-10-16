using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class StockService : IStockService
    {
        private readonly IStockRepository _repository;

        public StockService(IStockRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<StockDTO>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<int> InsertBulkAsync(IEnumerable<Stock> stocks)
        {
            return await _repository.InsertBulkAsync(stocks);
        }


        public async Task<StockDTO> GetById(long id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public Task Update(Stock stock)
        {
            return _repository.UpdateAsync(stock);
        }


        public async Task Delete(int id)
        {
            await _repository.DeactivateAsync(id);
        }
    }
}
