public interface IArAgingRepository
{
    Task<IEnumerable<ArAgingSummaryDto>> GetSummaryAsync(DateTime asOfDate);
    Task<IEnumerable<ArAgingInvoiceDto>> GetCustomerInvoicesAsync(int customerId, DateTime asOfDate);
}