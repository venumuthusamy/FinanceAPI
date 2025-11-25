using FinanceApi.Data;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FinanceReportController : ControllerBase
    {
        private readonly IFinanceReportService _service;

        public FinanceReportController(IFinanceReportService service)
        {
            _service = service;
        }

        [HttpPost("trial-balance")]
        public async Task<IActionResult> TrialBalance([FromBody] ReportBaseDTO dto)
        {
            var data = await _service.GetTrialBalanceAsync(dto);
            var result = new ResponseResult(true, "Success", data);
            return Ok(result);
        }
    }
}
