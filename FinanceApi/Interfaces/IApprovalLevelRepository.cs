using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IApprovalLevelRepository
    {
        Task<IEnumerable<ApprovalLevelDTO>> GetAllAsync();
        Task<ApprovalLevelDTO?> GetByIdAsync(int id);
        Task<ApprovalLevel> AddAsync(ApprovalLevel approvalLevel);
        Task<bool> UpdateAsync(ApprovalLevel approvalLevel);
        Task<bool> DeleteAsync(int id);
    }
}
