using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface ICountryService
    {
        Task<IEnumerable<Country>> GetAllAsync();
        Task<Country> GetById(int id);
        Task<int> CreateAsync(Country country);
        Task UpdateAsync(Country country);
        Task DeleteLicense(int id);
    }
}
