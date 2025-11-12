using FinanceApi.Data;
using FinanceApi.InterfaceService;
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
            int userId = 1; // TODO auth
            var id = await _svc.CreateAsync(userId, req);
            return Ok(new ResponseResult(true, "Sales Invoice created", id));
        }

        [HttpDelete("Delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _svc.DeleteAsync(id);
            return Ok(new ResponseResult(true, "Sales Invoice deleted", null));
        }

        // -------- EDIT (no currency) --------

        public class UpdateHeaderDto
        {
            public DateTime InvoiceDate { get; set; }
        }

        [HttpPut("UpdateHeader/{id:int}")]
        public async Task<IActionResult> UpdateHeader(int id, [FromBody] UpdateHeaderDto body)
        {
            if (body == null) return BadRequest(new ResponseResult(false, "Body required", null));
            int userId = 1;
            await _svc.UpdateHeaderAsync(id, body.InvoiceDate, userId);
            return Ok(new ResponseResult(true, "Header updated", null));
        }

        public class LineAddDto
        {
            public int? SourceLineId { get; set; }
            public int? ItemId { get; set; }
            public string? ItemName { get; set; }
            public string? Uom { get; set; }
            public decimal Qty { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal DiscountPct { get; set; }
            public int? TaxCodeId { get; set; }
        }

        [HttpPost("AddLine/{id:int}")]
        public async Task<IActionResult> AddLine(int id, [FromBody] LineAddDto l)
        {
            var hdr = await _svc.GetAsync(id);
            if (hdr == null) return Ok(new ResponseResult(false, "Invoice not found", null));

            var lineId = await _svc.AddLineAsync(id, new SiCreateLine
            {
                SourceLineId = l.SourceLineId,
                ItemId = l.ItemId ?? 0,
                ItemName = l.ItemName,
                Uom = l.Uom,
                Qty = l.Qty,
                UnitPrice = l.UnitPrice,
                DiscountPct = l.DiscountPct,
                TaxCodeId = l.TaxCodeId
            }, (byte)hdr.SourceType);

            return Ok(new ResponseResult(true, "Line added", lineId));
        }

        public class LineUpdateDto
        {
            public decimal Qty { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal DiscountPct { get; set; }
            public int? TaxCodeId { get; set; }
        }

        [HttpPut("UpdateLine/{lineId:int}")]
        public async Task<IActionResult> UpdateLine(int lineId, [FromBody] LineUpdateDto dto)
        {
            int userId = 1;
            await _svc.UpdateLineAsync(lineId, dto.Qty, dto.UnitPrice, dto.DiscountPct, dto.TaxCodeId, userId);
            return Ok(new ResponseResult(true, "Line updated", null));
        }

        [HttpDelete("RemoveLine/{lineId:int}")]
        public async Task<IActionResult> RemoveLine(int lineId)
        {
            await _svc.RemoveLineAsync(lineId);
            return Ok(new ResponseResult(true, "Line removed", null));
        }
    }
}
