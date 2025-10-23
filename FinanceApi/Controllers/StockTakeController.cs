using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockTakeController : ControllerBase
    {
        private readonly IStockTakeService _service;

        public StockTakeController(IStockTakeService service)
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

        [HttpGet("warehouse-items")]
        public async Task<IActionResult> GetWarehouseItems(
        [FromQuery] long warehouseId,
        [FromQuery] long supplierId,
        [FromQuery] byte takeTypeId,             // 1 = Full, 2 = Cycle  (required)
        [FromQuery] long? strategyId = null)    // strategyid is optional
        {
            // basic validation
            if (warehouseId <= 0) return BadRequest(new ResponseResult(false, "warehouseId is required.", null));
            if (takeTypeId != 1 && takeTypeId != 2)
                return BadRequest(new ResponseResult(false, "takeTypeId must be 1 (Full) or 2 (Cycle).", null));

            if (takeTypeId == 2 && strategyId is null)
                return BadRequest(new ResponseResult(false, "strategyId is required when takeTypeId = 2 (Cycle).", null));

            var items = await _service.GetWarehouseItemsAsync(warehouseId, supplierId,takeTypeId, strategyId);
            return Ok(new ResponseResult(true, "Success", items));
        }


        [HttpGet("get/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var licenseObj = await _service.GetByIdAsync(id);
            ResponseResult data = new ResponseResult(true, "Success", licenseObj);
            return Ok(data);
        }

        [HttpPost("insert")]
        public async Task<ActionResult> Create(StockTake stockTake)
        {
            var id = await _service.CreateAsync(stockTake);
            ResponseResult data = new ResponseResult(true, "StockTake created sucessfully", id);
            return Ok(data);
        }

        [HttpPut("update")]
        public async Task<IActionResult> Update(StockTake stockTake)
        {
            await _service.UpdateAsync(stockTake);
            ResponseResult data = new ResponseResult(true, "StockTake updated successfully.", null);
            return Ok(data);
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id, [FromQuery] int updatedBy)
        {
            await _service.DeleteLicense(id, updatedBy);
            ResponseResult data = new ResponseResult(true, "StockTake Deleted sucessfully", null);
            return Ok(data);
        }

        [HttpPost("{id:int}/post")]
        public async Task<IActionResult> PostInventory(int id, [FromBody] PostStockTakeRequest req)
        {
            var user = User?.Identity?.Name ?? "system";

            var count = await _service.CreateInventoryAdjustmentsFromStockTakeAsync(
                stockTakeId: id,
                reason: req?.Reason,
                remarks: req?.Remarks,
                userName: user,
                applyToStock: req?.ApplyToStock ?? true,
                markPosted: req?.MarkPosted ?? true,
                txnDateOverride: req?.TxnDate,
                onlySelected: req?.OnlySelected ?? true
            );

            var msg = count == 0 ? "No variances to post." : $"Posted {count} adjustment line(s).";
            return Ok(new ResponseResult(true, msg, new { Adjustments = count, StockTakeId = id }));
        }
    }
}
