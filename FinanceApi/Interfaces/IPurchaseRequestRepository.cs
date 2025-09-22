using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IPurchaseRequestRepository
    {
        Task<IEnumerable<PurchaseRequestDTO>> GetAllAsync();
        Task<PurchaseRequest?> GetByIdAsync(int id);
        Task<PurchaseRequest> AddAsync(PurchaseRequest pr);
        Task<bool> UpdateAsync(PurchaseRequest pr);
        Task<bool> DeleteAsync(int id);
    }
}
