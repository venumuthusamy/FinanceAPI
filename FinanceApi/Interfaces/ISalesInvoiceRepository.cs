using static FinanceApi.ModelDTO.SalesInvoiceDtos;

namespace FinanceApi.Interfaces
{
    public interface ISalesInvoiceRepository
    {
        Task<IEnumerable<SiSourceLineDto>> GetSourceLinesAsync(byte sourceType, int sourceId);
        Task<int> CreateAsync(int userId, SiCreateRequest req);
        Task<SiHeaderDto?> GetAsync(int id);
        Task<IEnumerable<SiLineDto>> GetLinesAsync(int id);
        Task<IEnumerable<SiListRowDto>> GetListAsync();
        Task DeactivateAsync(int id);
    }
}
