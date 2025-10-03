using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IPurchaseOrderRepository
    {
        Task<IEnumerable<PurchaseOrderDto>> GetAllAsync();
        Task<PurchaseOrderDto> GetByIdAsync(int id);
        Task<int> CreateAsync(PurchaseOrder purchaseOrder);
        Task UpdateAsync(PurchaseOrder purchaseOrder);
        Task DeactivateAsync(int id);
    }
}
