using FinanceApi.Interfaces;
using FinanceApi.Models;
using Microsoft.AspNetCore.Mvc;


namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SupplierGroupsController : ControllerBase
    {
        private readonly ISupplierGroupsService _service;

        public SupplierGroupsController(ISupplierGroupsService service)
        {
            _service = service;
        }

        [HttpGet("getAll")]
        public async Task<ActionResult<List<SupplierGroups>>> GetAll()
        {
            return Ok(await _service.GetAllAsync());
        }

        [HttpGet("get/{id}")]
        public async Task<ActionResult<SupplierGroups>> GetById(int id)
        {
            var customerGroups = await _service.GetByIdAsync(id);
            if (customerGroups == null) return NotFound();
            return Ok(customerGroups);
        }

        [HttpPost("insert")]
        public async Task<ActionResult<SupplierGroups>> Create(SupplierGroups supplierGroups)
        {
            try
            {
                var created = await _service.CreateAsync(supplierGroups);
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
        public async Task<ActionResult<SupplierGroups>> Update(int id, SupplierGroups supplierGroups)
        {
            var updated = await _service.UpdateAsync(id, supplierGroups);
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
