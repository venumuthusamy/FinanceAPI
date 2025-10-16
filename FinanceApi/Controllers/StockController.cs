using FinanceApi.Data;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StockController : ControllerBase
    {
        private readonly IStockService _service;

        public StockController(IStockService service)
        {
            _service = service;
        }


        [HttpGet("GetAllStock")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            ResponseResult data = new ResponseResult(true, "Success", list);
            return Ok(data);
        }



        [HttpPost("createStock")]
        public async Task<ActionResult> InsertStock([FromBody] List<Stock> stocks)
        {
            if (stocks == null || !stocks.Any())
                return BadRequest(new ResponseResult(false, "No stock data received", null));

            var inserted = await _service.InsertBulkAsync(stocks);

            var result = new ResponseResult(true, $"{inserted} stock record(s) inserted successfully.", inserted);
            return Ok(result);
        }



        [HttpGet("getStockById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var licenseObj = await _service.GetById(id);
            ResponseResult data = new ResponseResult(true, "Success", licenseObj);
            return Ok(data);
        }

        [HttpPut("updateStockById/{id}")]
        public async Task<IActionResult> Update(Stock stock)
        {
            await _service.Update(stock);
            ResponseResult data = new ResponseResult(true, "Stock updated successfully.", null);
            return Ok(data);
        }



        [HttpDelete("deleteStockById/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.Delete(id);
            ResponseResult data = new ResponseResult(true, "Stock Deleted sucessfully", null);
            return Ok(data);
        }
    }
}
