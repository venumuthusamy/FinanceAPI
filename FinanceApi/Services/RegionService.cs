using FinanceApi.Interfaces;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class RegionService : IRegionService
    {
        private readonly IRegionRepository _repository;

        public RegionService(IRegionRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<Region>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<Region?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<Region> CreateAsync(Region region)
        {

            return await _repository.CreateAsync(region);
        }

        public async Task<Region?> UpdateAsync(int id, Region region)
        {
            return await _repository.UpdateAsync(id, region);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }
    }
}
