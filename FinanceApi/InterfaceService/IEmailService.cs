using FinanceApi.ModelDTO;

namespace FinanceApi.InterfaceService
{
    public interface IEmailService
    {
        Task<bool> SendInvoiceEmailAsync(EmailRequestDto dto, byte[] pdfBytes);
    }
}
