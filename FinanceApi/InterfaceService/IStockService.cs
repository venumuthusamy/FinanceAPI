using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.InterfaceService
{
    public interface IStockService
    {
        Task<IEnumerable<StockDTO>> GetAllAsync();
        Task<int> CreateAsync(Stock stock);
        Task<StockDTO> GetById(long id);
        Task Update(Stock stock);
        Task Delete(int id);
    }
}
