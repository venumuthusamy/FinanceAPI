using FinanceApi.ModelDTO;

namespace FinanceApi.Interfaces
{
    public interface IPurchaseGoodReceiptService
    {
        Task<IEnumerable<PurchaseGoodReceiptItemsDTO>> GetAllAsync();

        Task<int> CreateAsync(PurchaseGoodReceiptItemsDTO goodReceiptItemsDTO);

        Task<PurchaseGoodReceiptItemsDTO> GetById(long id);
    }
}
