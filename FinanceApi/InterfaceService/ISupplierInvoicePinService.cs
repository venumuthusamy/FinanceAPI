using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.InterfaceService
{
    public interface ISupplierInvoicePinService
    {
        Task<IEnumerable<SupplierInvoicePin>> GetAllAsync();
        Task<SupplierInvoicePinDTO?> GetByIdAsync(int id);
        Task<int> CreateAsync(SupplierInvoicePin pin);
        Task UpdateAsync(SupplierInvoicePin pin);
        Task DeleteAsync(int id);
    }
}
