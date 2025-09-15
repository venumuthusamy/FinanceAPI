using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IIncomeRepository
    {
        Task<List<Income>> GetAllAsync();
        Task<Income?> GetByIdAsync(int id);
        Task<Income> CreateAsync(Income income);
        Task<Income?> UpdateAsync(int id, Income income);
        Task<bool> DeleteAsync(int id);
    }
}
