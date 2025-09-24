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

        public async Task<List<PurchaseOrderDto>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<PurchaseOrderDto?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<PurchaseOrder> CreateAsync(PurchaseOrder purchaseOrder)
        {
            // Example validation logic you might add later
            if (purchaseOrder.PoLines == null || !purchaseOrder.PoLines.Any())
                throw new ArgumentException("At least one line item is required.");

            return await _repository.CreateAsync(purchaseOrder);
        }

        public async Task<PurchaseOrder?> UpdateAsync(int id, PurchaseOrder purchaseOrder)
        {
            return await _repository.UpdateAsync(id, purchaseOrder);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }
    }
}
