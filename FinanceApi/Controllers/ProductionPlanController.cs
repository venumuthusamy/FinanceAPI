using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductionPlanController : ControllerBase
    {
        private readonly IProductionPlanRepository _repo;
        public ProductionPlanController(IProductionPlanRepository repo) => _repo = repo;

        // GET: api/production-planning/salesorders
        [HttpGet("salesorders")]
        public async Task<IActionResult> GetSalesOrders()
            => Ok(await _repo.GetSalesOrdersAsync());

        // GET: api/production-planning/so/5?warehouseId=1
        [HttpGet("so/{salesOrderId:int}")]
        public async Task<IActionResult> GetBySo(int salesOrderId, [FromQuery] int warehouseId)
            => Ok(await _repo.GetBySalesOrderAsync(salesOrderId, warehouseId));

        // POST: api/production-planning/save
        [HttpPost("save")]
        public async Task<IActionResult> Save([FromBody] SavePlanRequest req)
        {
            var id = await _repo.SavePlanAsync(req);
            return Ok(new { productionPlanId = id });
        }
        [HttpGet("list-with-lines")]
        public async Task<IActionResult> ListWithLines()
        {
            var data = await _repo.ListPlansWithLinesAsync();
            return Ok(new { isSuccess = true, data });
        }
    }
}
