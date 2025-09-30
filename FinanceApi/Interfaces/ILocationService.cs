using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface ILocationService
    {
        Task<IEnumerable<LocationDto>> GetAllAsync();
        Task<LocationDto> GetById(int id);
        Task<int> CreateAsync(Location location);
        Task UpdateAsync(Location location);
        Task DeleteAsync(int id);
    }
}
