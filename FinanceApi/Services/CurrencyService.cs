using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class CurrencyService : ICurrencyService
    {
        private readonly ICurrencyRepository _repository;

        public CurrencyService(ICurrencyRepository repository)
        {
            _repository = repository;
        }

        // GET ALL active currencies
        public Task<IEnumerable<CurrencyDTO>> GetAllAsync()
        {
            return _repository.GetAllAsync();
        }

        // GET currency by ID (only active)
        public Task<CurrencyDTO?> GetByIdAsync(int id)
        {
            return _repository.GetByIdAsync(id);
        }

        // CREATE a new currency
        public Task<Currency> CreateAsync(Currency currency)
        {
            return _repository.AddAsync(currency);
        }

        // UPDATE an existing currency
        public Task<bool> UpdateAsync(int id, Currency currency)
        {
            currency.Id = id;
            return _repository.UpdateAsync(currency);
        }

        // SOFT DELETE: mark IsActive = false
        public Task<bool> DeleteAsync(int id)
        {
            return _repository.DeleteAsync(id);
        }
    }

}
