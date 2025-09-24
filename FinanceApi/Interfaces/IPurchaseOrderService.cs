using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IPurchaseOrderService
    {
        Task<List<PurchaseOrderDto>> GetAllAsync();
        Task<PurchaseOrderDto?> GetByIdAsync(int id);
        Task<PurchaseOrder> CreateAsync(PurchaseOrder purchaseOrder);
        Task<PurchaseOrder?> UpdateAsync(int id, PurchaseOrder purchaseOrder);
        Task<bool> DeleteAsync(int id);
    }
}
