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
    }
}
