using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface ICountryRepository
    {
        Task<List<Country>> GetAllAsync();
        Task<Country?> GetByIdAsync(int id);
        Task<Country> CreateAsync(Country country);
        Task<Country?> UpdateAsync(int id, Country country);
        Task<bool> DeleteAsync(int id);
    }
}
