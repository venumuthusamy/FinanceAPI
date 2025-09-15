using FinanceApi.Interfaces;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class CityService : ICityService
    {
        private readonly ICityRepository _repository;

        public CityService(ICityRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<CityDto>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<CityDto?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<City> CreateAsync(City city)
        {

            return await _repository.CreateAsync(city);
        }

        public async Task<City?> UpdateAsync(int id, City city)
        {
            return await _repository.UpdateAsync(id, city);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }
    }
}
