using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IPurchaseOrderService
    {    
        Task<IEnumerable<PurchaseOrderDto>> GetAllAsync();
        Task<PurchaseOrderDto> GetByIdAsync(int id);
        Task<int> CreateAsync(PurchaseOrder purchaseOrder);
        Task UpdateAsync(PurchaseOrder purchaseOrder);
        Task DeleteLicense(int id);

        Task<IEnumerable<PurchaseOrderDto>> GetAllDetailswithGRN();
    }
}
