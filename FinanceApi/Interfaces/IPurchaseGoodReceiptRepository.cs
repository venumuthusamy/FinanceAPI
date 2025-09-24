using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IPurchaseGoodReceiptRepository
    {
        Task<IEnumerable<PurchaseGoodReceiptItemsDTO>> GetAllAsync();

        Task<PurchaseGoodReceiptItemsDTO> GetByIdAsync(long id);
        Task<int> CreateAsync(PurchaseGoodReceiptItemsDTO goodReceiptItemsDTO);
    }
}
