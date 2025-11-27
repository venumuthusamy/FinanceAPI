using System.IO;
using System.Threading.Tasks;
using FinanceApi.InterfaceService;
using Microsoft.AspNetCore.Hosting;

namespace FinanceApi.Services
{
    public class PdfService : IPdfService
    {
        private readonly IWebHostEnvironment _env;

        public PdfService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<byte[]> GenerateInvoicePdfAsync(string invoiceNo)
        {
            // This will be: C:\Users\...\FinanceApi\FinanceApi
            var projectRoot = _env.ContentRootPath;

            // ✅ This becomes:
            // C:\Users\...\FinanceApi\FinanceApi\wwwroot\pdf\SampleInvoice.pdf
            var pdfPath = Path.Combine(projectRoot, "wwwroot", "pdf", "SampleInvoice.pdf");

            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException($"PDF template not found at: {pdfPath}");
            }

            return await File.ReadAllBytesAsync(pdfPath);
        }
    }
}
