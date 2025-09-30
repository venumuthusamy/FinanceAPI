using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface ICityRepository
    {
        Task<IEnumerable<CityDto>> GetAllAsync();
        Task<CityDto> GetByIdAsync(long id);
        Task<int> CreateAsync(City city);
        Task UpdateAsync(City city);
        Task DeactivateAsync(int id);

        Task<CityDto> GetStateWithCountryId(long id);
    }
}
