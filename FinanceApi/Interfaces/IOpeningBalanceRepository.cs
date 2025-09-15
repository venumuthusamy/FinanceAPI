using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IOpeningBalanceRepository
    {
        Task<List<OpeningBalanceDto>> GetAllAsync();
        Task<OpeningBalanceDto?> GetByIdAsync(int id);
        Task<OpeningBalance> CreateAsync(OpeningBalance openingBalance);
        Task<OpeningBalance?> UpdateAsync(int id, OpeningBalance openingBalance);
        Task<bool> DeleteAsync(int id);
    }
}
