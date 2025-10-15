using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IStockRepository
    {
        Task<IEnumerable<StockDTO>> GetAllAsync();
        Task<StockDTO> GetByIdAsync(long id);
        Task<int> CreateAsync(Stock stock);
        Task UpdateAsync(Stock stock);
        Task DeactivateAsync(int id);
    }
}
