using FinanceApi.Data;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentTermsController : ControllerBase
    {
        private readonly IPaymentTermsService _service;

        public PaymentTermsController(IPaymentTermsService service)
        {
            _service = service;
        }

        // GET: api/PaymentTerms/GetPaymentTerms
        [HttpGet("GetPaymentTerms")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(new ResponseResult<IEnumerable<PaymentTermsDTO>>(true, "Payment terms retrieved successfully", result));
        }

        // GET: api/PaymentTerms/GetPaymentTermById/5
        [HttpGet("GetPaymentTermById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var paymentTerm = await _service.GetByIdAsync(id);
            if (paymentTerm == null)
                return NotFound(new ResponseResult<PaymentTermsDTO>(false, "Payment term not found", null));

            return Ok(new ResponseResult<PaymentTermsDTO>(true, "Payment term retrieved successfully", paymentTerm));
        }

        // POST: api/PaymentTerms/CreatePaymentTerm
        [HttpPost("CreatePaymentTerm")]
        public async Task<IActionResult> Create([FromBody] PaymentTerms paymentTerm)
        {
            if (paymentTerm == null)
                return BadRequest(new ResponseResult<PaymentTerms>(false, "Invalid data", null));

            paymentTerm.CreatedDate = DateTime.Now;
            var created = await _service.CreateAsync(paymentTerm);
            return Ok(new ResponseResult<PaymentTerms>(true, "Payment term created successfully", created));
        }

        // PUT: api/PaymentTerms/UpdatePaymentTermById/5
        [HttpPut("UpdatePaymentTermById/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] PaymentTerms paymentTerm)
        {
            if (paymentTerm == null)
                return BadRequest(new ResponseResult<PaymentTerms>(false, "Invalid data", null));

            var updated = await _service.UpdateAsync(id, paymentTerm);
            if (!updated)
                return NotFound(new ResponseResult<PaymentTerms>(false, "Payment term not found", null));

            return Ok(new ResponseResult<PaymentTerms>(true, "Payment term updated successfully", paymentTerm));
        }

        // DELETE: api/PaymentTerms/DeletePaymentTermById/5
        [HttpDelete("DeletePaymentTermById/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound(new ResponseResult<string>(false, "Payment term not found", null));

            return Ok(new ResponseResult<string>(true, "Payment term deleted successfully", null));
        }
    }
}
