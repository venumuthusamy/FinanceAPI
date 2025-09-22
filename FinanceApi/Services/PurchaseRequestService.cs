using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class PurchaseRequestService : IPurchaseRequestService
    {
        private readonly IPurchaseRequestRepository _repository;

        public PurchaseRequestService(IPurchaseRequestRepository repository)
        {
            _repository = repository;
        }

        public Task<IEnumerable<PurchaseRequestDTO>> GetAllAsync()
        {
            return _repository.GetAllAsync();
        }

        public Task<PurchaseRequest?> GetByIdAsync(int id)
        {
            return _repository.GetByIdAsync(id);
        }

        public Task<PurchaseRequest> CreateAsync(PurchaseRequest pr)
        {
            return _repository.AddAsync(pr);
        }

        public Task<bool> UpdateAsync(int id, PurchaseRequest pr)
        {
            pr.ID = id;
            return _repository.UpdateAsync(pr);
        }

        public Task<bool> DeleteAsync(int id)
        {
            return _repository.DeleteAsync(id);
        }
    }
}
