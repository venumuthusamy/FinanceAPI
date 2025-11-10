// Services/SalesInvoiceService.cs
using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using static FinanceApi.ModelDTO.SalesInvoiceDtos;

namespace FinanceApi.Services
{
    public class SalesInvoiceService : ISalesInvoiceService
    {
        private readonly ISalesInvoiceRepository _repo;
        public SalesInvoiceService(ISalesInvoiceRepository repo) => _repo = repo;

        public Task<IEnumerable<SiSourceLineDto>> GetSourceLinesAsync(byte sourceType, int sourceId)
            => _repo.GetSourceLinesAsync(sourceType, sourceId);

        public Task<int> CreateAsync(int userId, SiCreateRequest req)
            => _repo.CreateAsync(userId, req);

        public Task<SiHeaderDto?> GetAsync(int id) => _repo.GetAsync(id);

        public Task<IEnumerable<SiLineDto>> GetLinesAsync(int id) => _repo.GetLinesAsync(id);

        public Task<IEnumerable<SiListRowDto>> GetListAsync() => _repo.GetListAsync();

        public Task DeleteAsync(int id) => _repo.DeactivateAsync(id);
    }
}
