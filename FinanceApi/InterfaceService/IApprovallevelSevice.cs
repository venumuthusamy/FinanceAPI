using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.InterfaceService
{
    public interface IApprovallevelSevice
    {
        Task<IEnumerable<ApprovalLevelDTO>> GetAllAsync();
        Task<ApprovalLevelDTO> GetByIdAsync(int id);
        Task<ApprovalLevel> CreateAsync(ApprovalLevel approvalLevel);
        Task<bool> UpdateAsync(int id, ApprovalLevel approvalLevel);
        Task<bool> DeleteAsync(int id);
    }
}
