using FinanceApi.Data;
using FinanceApi.InterfaceService;
using FinanceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class SalesOrderController : ControllerBase
    {
        private readonly ISalesOrderService _service;

        public SalesOrderController(ISalesOrderService service)
        {
            _service = service;
        }

        [HttpGet("getAll")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            ResponseResult data = new ResponseResult(true, "Success", list);
            return Ok(data);
        }

        [HttpGet("get/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var licenseObj = await _service.GetByIdAsync(id);
            ResponseResult data = new ResponseResult(true, "Success", licenseObj);
            return Ok(data);
        }

        [HttpPost("insert")]
        public async Task<ActionResult> Create(SalesOrder salesOrder)
        {
            var id = await _service.CreateAsync(salesOrder);
            ResponseResult data = new ResponseResult(true, "Sales Order created sucessfully", id);
            return Ok(data);
        }

        [HttpPut("update")]
        public async Task<IActionResult> Update(SalesOrder salesOrder)
        {
            await _service.UpdateAsync(salesOrder);
            ResponseResult data = new ResponseResult(true, "Sales Order updated successfully.", null);
            return Ok(data);
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id, [FromQuery] int updatedBy)
        {
            await _service.DeleteLicense(id, updatedBy);
            ResponseResult data = new ResponseResult(true, "Sales Order Deleted sucessfully", null);
            return Ok(data);
        }

        [HttpGet("GetByQuatitonDetails/{id}")]
        public async Task<IActionResult> GetByQuatitonDetails(int id)
        {
            var licenseObj = await _service.GetByQuatitonDetails(id);
            ResponseResult data = new ResponseResult(true, "Success", licenseObj);
            return Ok(data);
        }

    }
}
