using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class ApprovalLevelService : IApprovallevelSevice
    {
        private readonly IApprovalLevelRepository _repository;

        public ApprovalLevelService(IApprovalLevelRepository repository)
        {
            _repository = repository;
        }

        public Task<IEnumerable<ApprovalLevelDTO>> GetAllAsync()
        {
            return _repository.GetAllAsync();
        }

        // GET ApprovalLevel by ID
        public Task<ApprovalLevelDTO> GetByIdAsync(int id)
        {
            return _repository.GetByIdAsync(id);
        }

        // CREATE ApprovalLevel
        public Task<ApprovalLevel> CreateAsync(ApprovalLevel approvalLevel)
        {
            return _repository.AddAsync(approvalLevel);
        }

        // UPDATE ApprovalLevel
        public Task<bool> UpdateAsync(int id, ApprovalLevel approvalLevel)
        {
            approvalLevel.Id = id;
            return _repository.UpdateAsync(approvalLevel);
        }

        // DELETE ApprovalLevel
        public Task<bool> DeleteAsync(int id)
        {
            return _repository.DeleteAsync(id);
        }
    }
}
