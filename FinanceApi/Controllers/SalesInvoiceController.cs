// Controllers/SalesInvoiceController.cs
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.Models;
using Microsoft.AspNetCore.Mvc;
using static FinanceApi.ModelDTO.SalesInvoiceDtos;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalesInvoiceController : ControllerBase
    {
        private readonly ISalesInvoiceService _svc;
        public SalesInvoiceController(ISalesInvoiceService svc) => _svc = svc;

        [HttpGet("List")]
        public async Task<IActionResult> List()
        {
            var rows = await _svc.GetListAsync();
            return Ok(new ResponseResult(true, "Sales Invoices retrieved", rows));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var hdr = await _svc.GetAsync(id);
            if (hdr == null) return Ok(new ResponseResult(false, "Not found", null));
            var lines = await _svc.GetLinesAsync(id);
            return Ok(new ResponseResult(true, "Success", new { header = hdr, lines }));
        }

        [HttpGet("SourceLines")]
        public async Task<IActionResult> SourceLines([FromQuery] byte sourceType, [FromQuery] int sourceId)
        {
            var rows = await _svc.GetSourceLinesAsync(sourceType, sourceId);
            return Ok(new ResponseResult(true, "Source lines", rows));
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] SiCreateRequest req)
        {
            // Swap with your auth context
            int userId = 1;
            var id = await _svc.CreateAsync(userId, req);
            return Ok(new ResponseResult(true, "Sales Invoice created", id));
        }

        [HttpDelete("Delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _svc.DeleteAsync(id);
            return Ok(new ResponseResult(true, "Sales Invoice deleted", null));
        }
    }
}
