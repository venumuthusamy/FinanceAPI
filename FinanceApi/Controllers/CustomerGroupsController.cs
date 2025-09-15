using FinanceApi.Interfaces;
using FinanceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomerGroupsController : ControllerBase
    {
        private readonly ICustomerGroupsService _service;

        public CustomerGroupsController(ICustomerGroupsService service)
        {
            _service = service;
        }

        [HttpGet("getAll")]
        public async Task<ActionResult<List<CustomerGroups>>> GetAll()
        {
            return Ok(await _service.GetAllAsync());
        }

        [HttpGet("get/{id}")]
        public async Task<ActionResult<CustomerGroups>> GetById(int id)
        {
            var customerGroups = await _service.GetByIdAsync(id);
            if (customerGroups == null) return NotFound();
            return Ok(customerGroups);
        }

        [HttpPost("insert")]
        public async Task<ActionResult<CustomerGroups>> Create(CustomerGroups customerGroups)
        {
            try
            {
                var created = await _service.CreateAsync(customerGroups);
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
        public async Task<ActionResult<CustomerGroups>> Update(int id, CustomerGroups customerGroups)
        {
            var updated = await _service.UpdateAsync(id, customerGroups);
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
