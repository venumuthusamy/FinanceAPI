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

        // GET: api/invoiceemail/template/1
        [HttpGet("template/{id}")]
        public async Task<IActionResult> GetTemplate(int id)
        {
            var tmpl = await _emailRepo.GetTemplateAsync(id);
            if (tmpl == null) return NotFound();

            return Ok(tmpl);
        }

        // POST: api/invoiceemail/send
        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] EmailRequestDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // generate invoice PDF
            var pdfBytes = await _pdfService.GenerateInvoicePdfAsync(dto.InvoiceNo);

            var success = await _emailService.SendInvoiceEmailAsync(dto, pdfBytes);

            var result = new EmailResultDto
            {
                Success = success,
                Message = success ? "Email sent successfully." : "Email failed."
            };

            return Ok(result);
        }
    }
}
