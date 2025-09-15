using FinanceApi.Interfaces;
using FinanceApi.Models;
using FinanceApi.Repositories;

namespace FinanceApi.Services
{
    public class ChartOfAccountService : IChartOfAccountService
    {
        private readonly IChartOfAccountRepository _repository;

        public ChartOfAccountService(IChartOfAccountRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<ChartOfAccount>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<ChartOfAccount?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<ChartOfAccount> CreateAsync(ChartOfAccount coa)
        {
            return await _repository.CreateAsync(coa);
        }

        public async Task<ChartOfAccount?> UpdateAsync(int id, ChartOfAccount coa)
        {
            return await _repository.UpdateAsync(id, coa);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }
    }
}
