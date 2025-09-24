using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface ICurrencyRepository
    {
        Task<IEnumerable<CurrencyDTO>> GetAllAsync();          
        Task<CurrencyDTO?> GetByIdAsync(int id);             
        Task<Currency> AddAsync(Currency currency);          
        Task<bool> UpdateAsync(Currency currency);         
        Task<bool> DeleteAsync(int id);
    }
}
