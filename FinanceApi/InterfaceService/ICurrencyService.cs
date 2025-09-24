using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.InterfaceService
{
    public interface ICurrencyService
    {
        Task<IEnumerable<CurrencyDTO>> GetAllAsync();
        Task<CurrencyDTO?> GetByIdAsync(int id);
        Task<Currency> CreateAsync(Currency currency);
        Task<bool> UpdateAsync(int id, Currency currency);
        Task<bool> DeleteAsync(int id);
    }
}
