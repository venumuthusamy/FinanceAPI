using FinanceApi.Interfaces;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class IncomeService : IIncomeService
    {
        private readonly IIncomeRepository _repository;

        public IncomeService(IIncomeRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<Income>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<Income?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<Income> CreateAsync(Income income)
        {

            return await _repository.CreateAsync(income);
        }

        public async Task<Income?> UpdateAsync(int id, Income income)
        {
            return await _repository.UpdateAsync(id, income);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }
    }
}
