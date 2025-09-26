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

        [HttpGet("GetSuppliers")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            ResponseResult data = new ResponseResult(true, "Supplier retrieved successfully", list);
            return Ok(data);
        }

        [HttpGet("GetSupplierById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var approvalLevel = await _service.GetById(id);
            if (approvalLevel == null)
            {
                ResponseResult data1 = new ResponseResult(false, "Supplier not found", null);
                return Ok(data1);
            }
            ResponseResult data = new ResponseResult(true, "Success", approvalLevel);
            return Ok(data);
        }




        [HttpPost("CreateSupplier")]
        public async Task<ActionResult> Create(Suppliers suppliers)
        {
            suppliers.CreatedDate = DateTime.Now;
            var id = await _service.CreateAsync(suppliers);
            ResponseResult data = new ResponseResult(true, "Supplier created successfully", id);
            return Ok(data);

        }

        [HttpPut("UpdateSupplierById/{id}")]
        public async Task<IActionResult> Update(Suppliers suppliers)
        {
            await _service.UpdateAsync(suppliers);
            ResponseResult data = new ResponseResult(true, "Supplier updated successfully.", null);
            return Ok(data);
        }

        [HttpDelete("DeleteSupplierById/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteLicense(id);
            ResponseResult data = new ResponseResult(true, "Supplier Deleted sucessfully", null);
            return Ok(data);
        }
    }
}
