using FinanceApi.ModelDTO;
using FinanceApi.Models;
using static FinanceApi.ModelDTO.AllocationPreviewRequest;

namespace FinanceApi.Interfaces
{
    public interface ISalesOrderRepository
    {
        Task<IEnumerable<SalesOrderDTO>> GetAllAsync();
        Task<SalesOrderDTO> GetByIdAsync(int id);
        Task<int> CreateAsync(SalesOrder salesOrder);
        Task UpdateAsync(SalesOrder salesOrder);
        Task DeactivateAsync(int id, int updatedBy);
        Task<QutationDetailsViewInfo?> GetByQuatitonDetails(int id);

        public Task<AllocationPreviewResponse> PreviewAllocationAsync(AllocationPreviewRequest req);

    }
}
