using FinanceApi.Interfaces;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class DeductionService : IDeductionService
    {

        private readonly IDeductionRepository _repository;

        public DeductionService(IDeductionRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<Deduction>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<Deduction?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<Deduction> CreateAsync(Deduction deduction)
        {

            return await _repository.CreateAsync(deduction);
        }

        public async Task<Deduction?> UpdateAsync(int id, Deduction deduction)
        {
            return await _repository.UpdateAsync(id, deduction);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }
    }
}
