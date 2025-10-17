using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IStockRepository
    {
        Task<IEnumerable<StockDTO>> GetAllAsync();
        Task<StockDTO> GetByIdAsync(long id);
        Task<int> InsertBulkAsync(IEnumerable<Stock> stocks);
        Task UpdateAsync(Stock stock);
        Task DeactivateAsync(int id);

        Task<IEnumerable<StockListViewInfo>> GetAllStockList();

        Task<int> MarkAsTransferredBulkAsync(IEnumerable<MarkAsTransferredRequest> requests);
        Task<IEnumerable<StockListViewInfo>> GetAllStockTransferedList();

        Task<int> AdjustOnHandAsync(AdjustOnHandRequest request);

        Task<IEnumerable<StockListViewInfo>> GetAllItemStockList();
    }
}
