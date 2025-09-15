using FinanceApi.Interfaces;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class OpeningBalanceService : IOpeningBalanceService
    {
        private readonly IOpeningBalanceRepository _repository;

        public OpeningBalanceService(IOpeningBalanceRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<OpeningBalanceDto>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<OpeningBalanceDto?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<OpeningBalance> CreateAsync(OpeningBalance openingBalance)
        {
            return await _repository.CreateAsync(openingBalance);
        }

        public async Task<OpeningBalance?> UpdateAsync(int id, OpeningBalance openingBalance)
        {
            return await _repository.UpdateAsync(id, openingBalance);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }
    }
}
