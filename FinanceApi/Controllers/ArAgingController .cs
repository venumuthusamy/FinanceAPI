using FinanceApi.Data;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class ArAgingController : ControllerBase
{
    private readonly IArAgingService _service;

    public ArAgingController(IArAgingService service)
    {
        _service = service;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.Today;
        var data = await _service.GetSummaryAsync(date);
        return Ok(new ResponseResult(true, "Success", data));
    }

    [HttpGet("detail/{customerId}")]
    public async Task<IActionResult> GetCustomerDetail(int customerId, [FromQuery] DateTime? asOfDate)
    {
        var date = asOfDate ?? DateTime.Today;
        var data = await _service.GetCustomerInvoicesAsync(customerId, date);
        return Ok(new ResponseResult(true, "Success", data));
    }
}
