using FinanceApi.Interfaces;
using FinanceApi.Models;
using FinanceApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class PurchaseOrderController : ControllerBase
    {
        private readonly IPurchaseOrderService _service;

        public PurchaseOrderController(IPurchaseOrderService service)
        {
            _service = service;
        }

        [HttpGet("getAll")]
        public async Task<ActionResult<List<PurchaseOrderDto>>> GetAll()
        {
            return Ok(await _service.GetAllAsync());
        }

        [HttpGet("get/{id}")]
        public async Task<ActionResult<PurchaseOrderDto>> GetById(int id)
        {
            var purchaseOrder = await _service.GetByIdAsync(id);
            if (purchaseOrder == null) return NotFound();
            return Ok(purchaseOrder);
        }

        [HttpPost("insert")]
        public async Task<ActionResult<PurchaseOrder>> Create(PurchaseOrder purchaseOrder)
        {
            try
            {
                var created = await _service.CreateAsync(purchaseOrder);
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
        public async Task<ActionResult<Sales>> Update(int id, PurchaseOrder purchaseOrder)
        {
            var updated = await _service.UpdateAsync(id, purchaseOrder);
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
