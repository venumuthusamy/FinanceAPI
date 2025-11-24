public class ArAgingService : IArAgingService
{
    private readonly IArAgingRepository _repo;

    public ArAgingService(IArAgingRepository repo)
    {
        _repo = repo;
    }

    public Task<IEnumerable<ArAgingSummaryDto>> GetSummaryAsync(DateTime asOfDate)
        => _repo.GetSummaryAsync(asOfDate);

    public Task<IEnumerable<ArAgingInvoiceDto>> GetCustomerInvoicesAsync(int customerId, DateTime asOfDate)
        => _repo.GetCustomerInvoicesAsync(customerId, asOfDate);
}
