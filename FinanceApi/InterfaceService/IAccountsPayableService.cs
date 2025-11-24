// InterfaceService/IAccountsPayableService.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using FinanceApi.ModelDTO;

namespace FinanceApi.InterfaceService
{
    public interface IAccountsPayableService
    {
        Task<IEnumerable<ApInvoiceDTO>> GetApInvoicesAsync();
        Task<IEnumerable<ApInvoiceDTO>> GetApInvoicesBySupplierAsync(int supplierId);
        Task<IEnumerable<ApMatchDTO>> GetMatchListAsync();
        Task<IEnumerable<ApPaymentListDto>> GetPaymentsAsync();
        Task<int> CreatePaymentAsync(ApPaymentCreateDto dto, int userId);
    }
}
