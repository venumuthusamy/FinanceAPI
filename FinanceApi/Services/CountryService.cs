using FinanceApi.Interfaces;
using FinanceApi.Models;


namespace FinanceApi.Services
{
    public class CountryService : ICountryService
    {
        private readonly ICountryRepository _repository;

        public CountryService(ICountryRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<Country>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<Country?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<Country> CreateAsync(Country country)
        {
            
            return await _repository.CreateAsync(country);
        }

        public async Task<Country?> UpdateAsync(int id, Country country)
        {
            return await _repository.UpdateAsync(id, country);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }
    }
}
