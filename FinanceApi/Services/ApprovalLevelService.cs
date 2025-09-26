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

        public async Task<IEnumerable<ApprovalLevelDTO>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<int> CreateAsync(ApprovalLevel approvalLevel)
        {
            return await _repository.CreateAsync(approvalLevel);

        }

        public async Task<ApprovalLevelDTO> GetById(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public Task UpdateAsync(ApprovalLevel approvalLevel)
        {
            return _repository.UpdateAsync(approvalLevel);
        }


        public async Task DeleteLicense(int id)
        {
            await _repository.DeactivateAsync(id);
        }
    }
}
