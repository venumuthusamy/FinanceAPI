using FinanceApi.Interfaces;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class LocationService : ILocationService
    {
        private readonly ILocationRepository _repository;

        public LocationService(ILocationRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<LocationDto>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<LocationDto?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<Location> CreateAsync(Location location)
        {
            return await _repository.CreateAsync(location);
        }

        public async Task<Location?> UpdateAsync(int id, Location location)
        {
            return await _repository.UpdateAsync(id, location);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }
    }
}
