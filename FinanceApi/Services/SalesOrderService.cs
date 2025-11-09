using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class SalesOrderService : ISalesOrderService
    {
        private readonly ISalesOrderRepository _repository;

        public SalesOrderService(ISalesOrderRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<SalesOrderDTO>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }
        public async Task<SalesOrderDTO?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<int> CreateAsync(SalesOrder salesOrder)
        {
            // Example validation logic you might add later
            if (salesOrder.LineItems == null || !salesOrder.LineItems.Any())
                throw new ArgumentException("At least one line item is required.");

            return await _repository.CreateAsync(salesOrder);
        }

        public async Task UpdateAsync(SalesOrder salesOrder)
        {
            await _repository.UpdateAsync(salesOrder);
        }

        public async Task DeleteLicense(int id, int updatedBy)
        {
            await _repository.DeactivateAsync(id, updatedBy);
        }
        public async Task<QutationDetailsViewInfo?> GetByQuatitonDetails(int id)
        {
            return await _repository.GetByQuatitonDetails(id);
        }
    }
}
