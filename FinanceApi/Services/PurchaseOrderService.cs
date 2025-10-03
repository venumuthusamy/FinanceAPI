using FinanceApi.Interfaces;
using FinanceApi.Models;
using FinanceApi.Repositories;

namespace FinanceApi.Services
{
    public class PurchaseOrderService : IPurchaseOrderService
    {
        private readonly IPurchaseOrderRepository _repository;

        public PurchaseOrderService(IPurchaseOrderRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<PurchaseOrderDto>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<PurchaseOrderDto?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<int> CreateAsync(PurchaseOrder purchaseOrder)
        {
            // Example validation logic you might add later
            if (purchaseOrder.PoLines == null || !purchaseOrder.PoLines.Any())
                throw new ArgumentException("At least one line item is required.");

            return await _repository.CreateAsync(purchaseOrder);
        }

        public async Task UpdateAsync(PurchaseOrder purchaseOrder)
        {
            await _repository.UpdateAsync(purchaseOrder);
        }

        public async Task DeleteLicense(int id)
        {
            await _repository.DeactivateAsync(id);
        }
    }
}
