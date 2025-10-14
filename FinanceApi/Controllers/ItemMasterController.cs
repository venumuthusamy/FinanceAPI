using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ItemMasterController : ControllerBase
    {
        private readonly IItemMasterService _service;
        public ItemMasterController(IItemMasterService service) { _service = service; }

        [HttpGet("GetItemMaster")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            var data = new ResponseResult(true, "ItemMaster list retrieved successfully", list);
            return Ok(data);
        }

        [HttpGet("GetItemMasterById/{id:int}")]
        public async Task<IActionResult> GetById([FromRoute] int id)
        {
            var item = await _service.GetByIdAsync(id);
            if (item == null)
                return Ok(new ResponseResult(false, "ItemMaster not found", null));
            return Ok(new ResponseResult(true, "Success", item));
        }

        [HttpPost("CreateItemMaster")]
        public async Task<IActionResult> Create([FromBody] ItemMaster item)
        {
            // minimal validation
            if (string.IsNullOrWhiteSpace(item.Sku) || string.IsNullOrWhiteSpace(item.Name))
                return BadRequest(new ResponseResult(false, "SKU and Name are required", null));

            var id = await _service.CreateAsync(item);
            return Ok(new ResponseResult(true, "ItemMaster created successfully", id));
        }

        [HttpPut("UpdateItemMasterById/{id:int}")]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] ItemMaster item)
        {
            if (id != item.Id) item.Id = id;
            await _service.UpdateAsync(item);
            return Ok(new ResponseResult(true, "ItemMaster updated successfully.", null));
        }

        [HttpDelete("DeleteItemMasterById/{id:int}")]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            await _service.DeleteAsync(id);
            return Ok(new ResponseResult(true, "ItemMaster deleted successfully", null));
        }
    }
}
