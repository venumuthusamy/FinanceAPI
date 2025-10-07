using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class SupplierInvoicePinService : ISupplierInvoicePinService
    {
        private readonly ISupplierInvoicePinRepository _repo;
        public SupplierInvoicePinService(ISupplierInvoicePinRepository repo) => _repo = repo;

        public Task<IEnumerable<SupplierInvoicePin>> GetAllAsync() => _repo.GetAllAsync();
        public Task<SupplierInvoicePinDTO?> GetByIdAsync(int id) => _repo.GetByIdAsync(id);
        public Task<int> CreateAsync(SupplierInvoicePin pin) => _repo.CreateAsync(pin);
        public Task UpdateAsync(SupplierInvoicePin pin) => _repo.UpdateAsync(pin);
        public Task DeleteAsync(int id) => _repo.DeactivateAsync(id);
    }
}
