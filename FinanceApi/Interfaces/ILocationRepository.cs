using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface ILocationRepository
    {
        Task<List<LocationDto>> GetAllAsync();
        Task<LocationDto?> GetByIdAsync(int id);
        Task<Location> CreateAsync(Location location);
        Task<Location?> UpdateAsync(int id, Location location);
        Task<bool> DeleteAsync(int id);
    }
}
