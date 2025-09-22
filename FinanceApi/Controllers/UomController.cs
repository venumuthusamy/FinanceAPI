using FinanceApi.Interfaces;
using FinanceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UomController : ControllerBase
    {
        private readonly IUomService _service;

        public UomController(IUomService service)
        {
            _service = service;
        }

        [HttpGet("getAll")]
        public async Task<ActionResult<List<Uom>>> GetAll()
        {
            return Ok(await _service.GetAllAsync());
        }

        [HttpGet("get/{id}")]
        public async Task<ActionResult<Uom>> GetById(int id)
        {
            var uom = await _service.GetByIdAsync(id);
            if (uom == null) return NotFound();
            return Ok(uom);
        }

        [HttpPost("insert")]
        public async Task<ActionResult<Uom>> Create(Uom uom)
        {
            try
            {
                var created = await _service.CreateAsync(uom);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (ArgumentException ex)
            {
                // Return 400 Bad Request with just the message
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                // For unexpected exceptions, return 500 Internal Server Error
                return StatusCode(500, "An unexpected error occurred: " + ex.Message);
            }


        }

        [HttpPut("update/{id}")]
        public async Task<ActionResult<Uom>> Update(int id, Uom uom)
        {
            var updated = await _service.UpdateAsync(id, uom);
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
