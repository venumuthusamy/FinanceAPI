using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using System.Data;

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

        public async Task<IEnumerable<StockListViewInfo>> GetAllStockList()
        {
            return await _repository.GetAllStockList();
        }

        public async Task<IEnumerable<StockListViewInfo>> GetAllItemStockList()
        {
            return await _repository.GetAllItemStockList();
        }

        public async Task<int> MarkAsTransferredBulkAsync(IEnumerable<MarkAsTransferredRequest> requests)
        {
            return await _repository.MarkAsTransferredBulkAsync(requests);
        }

        public async Task<IEnumerable<StockTransferListViewInfo>> GetAllStockTransferedList()
        {
            return await _repository.GetAllStockTransferedList();
        }
        public async Task<IEnumerable<StockTransferListViewInfo>> GetStockTransferedList()
        {
            return await _repository.GetStockTransferedList();
        }

        public async Task<int> AdjustOnHandAsync(AdjustOnHandRequest request)
        {
            return await _repository.AdjustOnHandAsync(request);
        }

        public async Task<ApproveBulkResult> ApproveTransfersBulkAsync(IEnumerable<ApproveTransferRequest> requests)
        {
            return await _repository.ApproveTransfersBulkAsync(requests);
        }

        public async Task<StockHistoryViewInfo> GetByIdStockHistory(long id)
        {
            return await _repository.GetByIdStockHistory(id);
        }
    }
}
