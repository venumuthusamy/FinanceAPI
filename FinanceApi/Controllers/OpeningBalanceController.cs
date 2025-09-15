using FinanceApi.Interfaces;
using FinanceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OpeningBalanceController : ControllerBase
    {
        private readonly IOpeningBalanceService _service;

        public OpeningBalanceController(IOpeningBalanceService service)
        {
            _service = service;
        }

        [HttpGet("getAll")]
        public async Task<ActionResult<List<OpeningBalanceDto>>> GetAll()
        {
            return Ok(await _service.GetAllAsync());
        }

        [HttpGet("get/{id}")]
        public async Task<ActionResult<OpeningBalanceDto>> GetById(int id)
        {
            var openingBalance = await _service.GetByIdAsync(id);
            if (openingBalance == null) return NotFound();
            return Ok(openingBalance);
        }

        [HttpPost("insert")]
        public async Task<ActionResult<OpeningBalance>> Create(OpeningBalance openingBalance)
        {
            try
            {
                var created = await _service.CreateAsync(openingBalance);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An unexpected error occurred: " + ex.Message);
            }
        }

        [HttpPut("update/{id}")]
        public async Task<ActionResult<OpeningBalance>> Update(int id, OpeningBalance openingBalance)
        {
            var updated = await _service.UpdateAsync(id, openingBalance);
            if (updated == null) return NotFound();
            return (updated);
        }

        [HttpDelete("delete/{id}")]
        public async Task<ActionResult<OpeningBalance>> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted) return NotFound();
            return NoContent();

        }
    }
}
