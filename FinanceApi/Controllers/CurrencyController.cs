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
    public class CurrencyController : ControllerBase
    {
        private readonly ICurrencyService _service;

        public CurrencyController(ICurrencyService service)
        {
            _service = service;
        }

        // GET: api/Currency/GetCurrencies
        [HttpGet("GetCurrencies")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(new ResponseResult<IEnumerable<CurrencyDTO>>(true, "Currencies retrieved successfully", result));
        }

        // GET: api/Currency/GetCurrencyById/5
        [HttpGet("GetCurrencyById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var currency = await _service.GetByIdAsync(id);
            if (currency == null)
                return NotFound(new ResponseResult<CurrencyDTO>(false, "Currency not found", null));

            return Ok(new ResponseResult<CurrencyDTO>(true, "Currency retrieved successfully", currency));
        }

        // POST: api/Currency/CreateCurrency
        [HttpPost("CreateCurrency")]
        public async Task<IActionResult> Create([FromBody] Currency currency)
        {
            if (currency == null)
                return BadRequest(new ResponseResult<Currency>(false, "Invalid data", null));

            currency.CreatedDate = DateTime.Now;
            var created = await _service.CreateAsync(currency);
            return Ok(new ResponseResult<Currency>(true, "Currency created successfully", created));
        }

        // PUT: api/Currency/UpdateCurrencyById/5
        [HttpPut("UpdateCurrencyById/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Currency currency)
        {
            if (currency == null)
                return BadRequest(new ResponseResult<Currency>(false, "Invalid data", null));

            var updated = await _service.UpdateAsync(id, currency);
            if (!updated)
                return NotFound(new ResponseResult<Currency>(false, "Currency not found", null));

            return Ok(new ResponseResult<Currency>(true, "Currency updated successfully", currency));
        }

        // DELETE: api/Currency/DeleteCurrencyById/5
        [HttpDelete("DeleteCurrencyById/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound(new ResponseResult<string>(false, "Currency not found", null));

            return Ok(new ResponseResult<string>(true, "Currency deleted successfully", null));
        }
    }
}
