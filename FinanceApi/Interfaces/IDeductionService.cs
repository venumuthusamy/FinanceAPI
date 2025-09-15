using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IDeductionService
    {
        Task<List<Deduction>> GetAllAsync();
        Task<Deduction?> GetByIdAsync(int id);
        Task<Deduction> CreateAsync(Deduction deduction);
        Task<Deduction?> UpdateAsync(int id, Deduction deduction);
        Task<bool> DeleteAsync(int id);
    }
}
