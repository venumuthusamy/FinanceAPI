// Controllers/AccountsPayableController.cs
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/finance/ap")]
    public class AccountsPayableController : ControllerBase
    {
        private readonly IAccountsPayableRepository _apRepo;
        private readonly ISupplierPaymentRepository _paymentRepo;

        public AccountsPayableController(
            IAccountsPayableRepository apRepo,
            ISupplierPaymentRepository paymentRepo)
        {
            _apRepo = apRepo;
            _paymentRepo = paymentRepo;
        }

        // ---------- INVOICES ----------
        [HttpGet("invoices")]
        public async Task<IActionResult> GetApInvoices()
        {
            var rows = await _apRepo.GetApInvoicesAsync();
            return Ok(new { isSuccess = true, data = rows });
        }

        [HttpGet("invoices/supplier/{supplierId:int}")]
        public async Task<IActionResult> GetApInvoicesBySupplier(int supplierId)
        {
            var rows = await _apRepo.GetApInvoicesBySupplierAsync(supplierId);
            return Ok(new { isSuccess = true, data = rows });
        }

        // ---------- PAYMENTS ----------
        [HttpGet("payments")]
        public async Task<IActionResult> GetPayments()
        {
            var rows = await _paymentRepo.GetAllAsync();
            return Ok(new { isSuccess = true, data = rows });
        }

        [HttpPost("payments")]
        public async Task<IActionResult> CreatePayment([FromBody] SupplierPaymentCreateDTO dto)
        {
            if (dto == null)
                return BadRequest(new { isSuccess = false, message = "Payload missing" });

            try
            {
                var ok = await _paymentRepo.CreateAsync(dto);
                return Ok(new
                {
                    isSuccess = ok,
                    message = ok ? "Payment posted" : "Failed to post payment"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { isSuccess = false, message = ex.Message });
            }
        }

        // ---------- 3-WAY MATCH ----------
        [HttpGet("match")]
        public async Task<IActionResult> GetMatchList()
        {
            var rows = await _apRepo.GetMatchListAsync();
            return Ok(new { isSuccess = true, data = rows });
        }
    }
}
