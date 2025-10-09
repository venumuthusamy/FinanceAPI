using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IPurchaseRequestRepository
    {
        Task<IEnumerable<PurchaseRequestDTO>> GetAllAsync();
        Task<PurchaseRequestDTO> GetByIdAsync(int id);
        Task<IEnumerable<PurchaseRequestDTO>> GetAvailablePurchaseRequestsAsync();
        Task<int> CreateAsync(PurchaseRequest pr);
        Task UpdateAsync(PurchaseRequest pr);
        Task DeactivateAsync(int id);
    }
}
