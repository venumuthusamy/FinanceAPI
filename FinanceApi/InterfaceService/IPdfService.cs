namespace FinanceApi.InterfaceService
{
    public interface IPdfService
    {
        Task<byte[]> GenerateInvoicePdfAsync(string invoiceNo);
    }
}
