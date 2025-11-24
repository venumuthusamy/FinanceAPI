// Services/AccountsPayableService.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using FinanceApi.InterfaceService;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;

namespace FinanceApi.Services
{
    public class AccountsPayableService : IAccountsPayableService
    {
        private readonly IAccountsPayableRepository _repo;
        private readonly IPeriodCloseService _periodLock;

        public AccountsPayableService(
            IAccountsPayableRepository repo,
            IPeriodCloseService periodLock)
        {
            _repo = repo;
            _periodLock = periodLock;
        }

        public Task<IEnumerable<ApInvoiceDTO>> GetApInvoicesAsync()
            => _repo.GetApInvoicesAsync();

        public Task<IEnumerable<ApInvoiceDTO>> GetApInvoicesBySupplierAsync(int supplierId)
            => _repo.GetApInvoicesBySupplierAsync(supplierId);

        public Task<IEnumerable<ApMatchDTO>> GetMatchListAsync()
            => _repo.GetMatchListAsync();

        public Task<IEnumerable<ApPaymentListDto>> GetPaymentsAsync()
            => _repo.GetPaymentsAsync();

        public async Task<int> CreatePaymentAsync(ApPaymentCreateDto dto, int userId)
        {
            // 🔒 Period lock check
            await _periodLock.EnsureOpenAsync(dto.PaymentDate);

            // ✅ Insert payment
            return await _repo.CreatePaymentAsync(dto, userId);
        }
    }
}
