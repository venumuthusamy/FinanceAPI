using FinanceApi.Interfaces;
using FinanceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChartOfAccountController : ControllerBase
    {
        private readonly IChartOfAccountService _service;

        public ChartOfAccountController(IChartOfAccountService service)
        {
            _service = service;
        }

        [HttpGet("getAll")]
        public async Task<ActionResult<List<ChartOfAccount>>> GetAll()
        {
            return Ok(await _service.GetAllAsync());
        }

        [HttpGet("get/{id}")]
        public async Task<ActionResult<ChartOfAccount>> GetById(int id)
        {
            var coa = await _service.GetByIdAsync(id);
            if (coa == null) return NotFound();
            return Ok(coa);
        }

        [HttpPost("insert")]
        public async Task<ActionResult<State>> Create(ChartOfAccount coa)
        {
            try
            {
                var created = await _service.CreateAsync(coa);
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
        public async Task<ActionResult<State>> Update(int id, ChartOfAccount coa)
        {
            var updated = await _service.UpdateAsync(id, coa);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted) return NotFound();
            return NoContent();
        }
    }
}
