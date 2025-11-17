using FinanceApi.ModelDTO;

namespace FinanceApi.Interfaces
{
    public interface IAccountsPayableRepository
    {
        Task<IEnumerable<ApInvoiceDTO>> GetApInvoicesAsync();
        Task<IEnumerable<ApInvoiceDTO>> GetApInvoicesBySupplierAsync(int supplierId);
        Task<IEnumerable<ApMatchDTO>> GetMatchListAsync();
    }
}
