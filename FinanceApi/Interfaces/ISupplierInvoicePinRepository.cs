using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface ISupplierInvoicePinRepository
    {
        Task<IEnumerable<SupplierInvoicePin>> GetAllAsync();
        Task<SupplierInvoicePinDTO?> GetByIdAsync(int id);
        Task<int> CreateAsync(SupplierInvoicePin pin);
        Task UpdateAsync(SupplierInvoicePin pin);
        Task DeactivateAsync(int id);
    }
}
