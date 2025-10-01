using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface ICityService
    {
        Task<IEnumerable<CityDto>> GetAllAsync();
        Task<CityDto> GetById(int id);
        Task<int> CreateAsync(City city);
        Task UpdateAsync(City city);
        Task DeleteAsync(int id);
        Task<IEnumerable<CityDto>> GetStateWithCountryId(int id);
        Task<IEnumerable<CityDto>> GetCityWithStateId(int id);
    }
}
