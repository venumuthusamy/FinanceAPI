using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IChartOfAccountRepository
    {
        Task<List<ChartOfAccount>> GetAllAsync();
        Task<ChartOfAccount?> GetByIdAsync(int id);
        Task<ChartOfAccount> CreateAsync(ChartOfAccount chartOfAccount);
        Task<ChartOfAccount?> UpdateAsync(int id, ChartOfAccount chartOfAccount);
        Task<bool> DeleteAsync(int id);
    }
}
