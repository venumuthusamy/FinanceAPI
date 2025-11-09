using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.InterfaceService
{
    public interface ISalesOrderService
    {
        Task<IEnumerable<SalesOrderDTO>> GetAllAsync();
        Task<SalesOrderDTO> GetByIdAsync(int id);
        Task<int> CreateAsync(SalesOrder salesOrder);
        Task UpdateAsync(SalesOrder salesOrder);
        Task DeleteLicense(int id, int updatedBy);

        Task<QutationDetailsViewInfo?> GetByQuatitonDetails(int id);

    }
}
