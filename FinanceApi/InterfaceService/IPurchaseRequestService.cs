using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.InterfaceService
{
    public interface IPurchaseRequestService
    {
        Task<IEnumerable<PurchaseRequestDTO>> GetAllAsync();
        Task<PurchaseRequestDTO> GetById(int id);
        Task<int> CreateAsync(PurchaseRequest purchaseRequest);
        Task UpdateAsync(PurchaseRequest purchaseRequest);
        Task DeleteLicense(int id);
    }
}
