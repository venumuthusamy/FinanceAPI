using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SuppliersController : ControllerBase
    {
        private readonly ISuppliersService _service;

        public SuppliersController(ISuppliersService service)
        {
            _service = service;
        }

        // GET: api/Suppliers/GetSuppliers
        [HttpGet("GetSuppliers")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(new ResponseResult<IEnumerable<SuppliersDTO>>(true, "Suppliers retrieved successfully", result));
        }

        // GET: api/Suppliers/GetSupplierById/5
        [HttpGet("GetSupplierById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var supplier = await _service.GetByIdAsync(id);
            if (supplier == null)
                return NotFound(new ResponseResult<SuppliersDTO>(false, "Supplier not found", null));

            return Ok(new ResponseResult<SuppliersDTO>(true, "Supplier retrieved successfully", supplier));
        }

        // POST: api/Suppliers/CreateSupplier
        [HttpPost("CreateSupplier")]
        public async Task<IActionResult> Create([FromBody] Suppliers supplier)
        {
            if (supplier == null)
                return BadRequest(new ResponseResult<Suppliers>(false, "Invalid data", null));

            supplier.CreatedDate = DateTime.UtcNow;
            var created = await _service.CreateAsync(supplier);
            return Ok(new ResponseResult<Suppliers>(true, "Supplier created successfully", created));
        }

        // PUT: api/Suppliers/UpdateSupplierById/5
        [HttpPut("UpdateSupplierById/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Suppliers supplier)
        {
            if (supplier == null)
                return BadRequest(new ResponseResult<Suppliers>(false, "Invalid data", null));

            var updated = await _service.UpdateAsync(id, supplier);
            if (!updated)
                return NotFound(new ResponseResult<Suppliers>(false, "Supplier not found", null));

            return Ok(new ResponseResult<Suppliers>(true, "Supplier updated successfully", supplier));
        }

        // DELETE: api/Suppliers/DeleteSupplierById/5
        [HttpDelete("DeleteSupplierById/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound(new ResponseResult<string>(false, "Supplier not found", null));

            return Ok(new ResponseResult<string>(true, "Supplier deleted successfully", null));
        }
    }
}
