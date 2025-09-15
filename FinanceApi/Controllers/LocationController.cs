using FinanceApi.Interfaces;
using FinanceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LocationController : ControllerBase
    {
        private readonly ILocationService _service;

        public LocationController(ILocationService service)
        {
            _service = service;
        }

        [HttpGet("getAll")]
        public async Task<ActionResult<List<LocationDto>>> GetAll()
        {
            return Ok(await _service.GetAllAsync());
        }

        [HttpGet("get/{id}")]
        public async Task<ActionResult<LocationDto>> GetById(int id)
        {
            var location = await _service.GetByIdAsync(id);
            if (location == null) return NotFound();
            return Ok(location);
        }

        [HttpPost("insert")]
        public async Task<ActionResult<Location>> Create(Location location)
        {
            try
            {
                var created = await _service.CreateAsync(location);
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
        public async Task<ActionResult<Location>> Update(int id, Location location)
        {
            var updated = await _service.UpdateAsync(id, location);
            if (updated == null) return NotFound();
            return (updated);
        }

        [HttpDelete("delete/{id}")]
        public async Task<ActionResult<Location>> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted) return NotFound();
            return NoContent();

        }
    }
}
