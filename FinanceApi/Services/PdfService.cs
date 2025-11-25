using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;

namespace FinanceApi.Services
{
    public class PdfService : IPdfService
    {
        public async Task<byte[]> GenerateInvoicePdfAsync(string invoiceNo)
        {
            // TODO: Replace with real invoice PDF creation
            // For testing you can load a static file
            var path = Path.Combine(AppContext.BaseDirectory, "SampleInvoice.pdf");
            var bytes = await File.ReadAllBytesAsync(path);
            return bytes;
        }
    }
}
