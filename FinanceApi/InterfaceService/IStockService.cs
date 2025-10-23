using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.InterfaceService
{
    public interface IStockService
    {
        Task<IEnumerable<StockDTO>> GetAllAsync();
        Task<int> InsertBulkAsync(IEnumerable<Stock> stocks);
        Task<StockDTO> GetById(long id);
        Task Update(Stock stock);
        Task Delete(int id);

        Task<IEnumerable<StockListViewInfo>> GetAllStockList();
        Task<int> MarkAsTransferredBulkAsync(IEnumerable<MarkAsTransferredRequest> requests);
        Task<IEnumerable<StockTransferListViewInfo>> GetAllStockTransferedList();

        Task<int> AdjustOnHandAsync(AdjustOnHandRequest request);

        Task<IEnumerable<StockListViewInfo>> GetAllItemStockList();

        Task<ApproveBulkResult> ApproveTransfersBulkAsync(IEnumerable<ApproveTransferRequest> requests);

        Task<StockHistoryViewInfo> GetByIdStockHistory(long id);
    }
}
