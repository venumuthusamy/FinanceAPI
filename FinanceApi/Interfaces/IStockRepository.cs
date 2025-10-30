using FinanceApi.ModelDTO;
using FinanceApi.Models;
using System.Data;

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
        Task<IEnumerable<StockTransferListViewInfo>> GetAllStockTransferedList();

        Task<AdjustOnHandResult> AdjustOnHandAsync(AdjustOnHandRequest request, IDbTransaction? tx = null);

        Task<IEnumerable<StockListViewInfo>> GetAllItemStockList();

        Task<ApproveBulkResult> ApproveTransfersBulkAsync(IEnumerable<ApproveTransferRequest> requests);

        Task<StockHistoryViewInfo> GetByIdStockHistory(long id);
    }
}
