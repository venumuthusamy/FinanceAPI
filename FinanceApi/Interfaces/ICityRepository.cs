using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface ICityRepository
    {
        Task<List<CityDto>> GetAllAsync();
        Task<CityDto?> GetByIdAsync(int id);
        Task<City> CreateAsync(City city);
        Task<City?> UpdateAsync(int id, City city);
        Task<bool> DeleteAsync(int id);
    }
}
