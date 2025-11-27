using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InvoiceEmailController : ControllerBase
    {
        private readonly InterfaceService.IEmailService _emailService;
        private readonly IPdfService _pdfService;
        private readonly IEmailRepository _emailRepo;

        public InvoiceEmailController(
            InterfaceService.IEmailService emailService,
            IPdfService pdfService,
            IEmailRepository emailRepo)
        {
            _emailService = emailService;
            _pdfService = pdfService;
            _emailRepo = emailRepo;
        }

        // GET: api/invoiceemail/invoices?docType=SI
        [HttpGet("invoices")]
        public async Task<IActionResult> GetInvoices([FromQuery] string docType)
        {
            if (string.IsNullOrWhiteSpace(docType))
                return BadRequest("docType is required (SI or PIN).");

            docType = docType.ToUpperInvariant();
            if (docType != "SI" && docType != "PIN")
                return BadRequest("Invalid docType. Use 'SI' or 'PIN'.");

            var list = await _emailRepo.GetInvoiceListAsync(docType);
            return Ok(list);
        }

        // GET: api/invoiceemail/invoiceinfo/SI/123
        [HttpGet("invoiceinfo/{docType}/{invoiceId:int}")]
        public async Task<IActionResult> GetInvoiceInfo(string docType, int invoiceId)
        {
            if (invoiceId <= 0)
                return BadRequest("Invalid invoice id.");

            docType = docType.ToUpperInvariant();
            if (docType != "SI" && docType != "PIN")
                return BadRequest("Invalid docType. Use 'SI' or 'PIN'.");

            var info = await _emailRepo.GetInvoiceInfoAsync(docType, invoiceId);
            if (info == null)
                return NotFound("Invoice not found.");

            return Ok(info);
        }

        // GET: api/invoiceemail/template/1?docType=SI
        [HttpGet("template/{id:int}")]
        public async Task<IActionResult> GetTemplate(int id, [FromQuery] string? docType = null)
        {
            if (!string.IsNullOrWhiteSpace(docType))
                docType = docType.ToUpperInvariant();

            var tmpl = await _emailRepo.GetTemplateAsync(id, docType);
            if (tmpl == null) return NotFound();

            return Ok(tmpl);
        }
        [HttpPost("send")]
        public async Task<ActionResult<EmailResultDto>> Send([FromBody] EmailRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new EmailResultDto
                {
                    Success = false,
                    Message = "Invalid email request"
                });

            // Example: fileName includes invoice number
            var pdfBytes = await _pdfService.GenerateInvoicePdfAsync(dto.FileName);

            var result = await _emailService.SendInvoiceEmailAsync(dto, pdfBytes);

            if (!result.Success)
                return StatusCode(500, result);

            return Ok(result);
        }
    }
}
