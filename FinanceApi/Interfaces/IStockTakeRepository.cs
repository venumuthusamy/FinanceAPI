using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IStockTakeRepository
    {
        Task<IEnumerable<StockTakeDTO>> GetAllAsync();
        Task<StockTakeDTO> GetByIdAsync(int id);
        Task<int> CreateAsync(StockTake stockTake);
        Task UpdateAsync(StockTake stockTake);
        Task DeactivateAsync(int id);
    }
}
