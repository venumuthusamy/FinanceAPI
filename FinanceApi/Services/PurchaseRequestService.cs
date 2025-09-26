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

        public async Task<IEnumerable<PurchaseRequestDTO>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<int> CreateAsync(PurchaseRequest purchaseRequest)
        {
            return await _repository.CreateAsync(purchaseRequest);

        }

        public async Task<PurchaseRequestDTO> GetById(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public Task UpdateAsync(PurchaseRequest purchaseRequest)
        {
            return _repository.UpdateAsync(purchaseRequest);
        }


        public async Task DeleteLicense(int id)
        {
            await _repository.DeactivateAsync(id);
        }
    }
}
