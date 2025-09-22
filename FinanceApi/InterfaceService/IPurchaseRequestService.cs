using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.InterfaceService
{
    public interface IPurchaseRequestService
    {
        Task<IEnumerable<PurchaseRequestDTO>> GetAllAsync();
        Task<PurchaseRequest?> GetByIdAsync(int id);
        Task<PurchaseRequest> CreateAsync(PurchaseRequest pr);
        Task<bool> UpdateAsync(int id, PurchaseRequest pr);
        Task<bool> DeleteAsync(int id);
    }
}
