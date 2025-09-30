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

        public async Task<IEnumerable<CityDto>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<CityDto> GetById(int id)
        {
            return await _repository.GetByIdAsync(id);
        }
        public async Task<int> CreateAsync(City city)
        {
            return await _repository.CreateAsync(city);

        }



        public Task UpdateAsync(City city)
        {
            return _repository.UpdateAsync(city);
        }


        public async Task DeleteAsync(int id)
        {
            await _repository.DeactivateAsync(id);
        }

        public async Task<CityDto> GetStateWithCountryId(int id)
        {
            return await _repository.GetStateWithCountryId(id);
        }
    }
}
