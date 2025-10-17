using FinanceApi.ModelDTO;

namespace InterfaceService
{
    public interface IItemMasterService
    {
        Task<IEnumerable<ItemMasterDTO>> GetAllAsync();
        Task<ItemMasterDTO?> GetByIdAsync(int id);
        Task<long> CreateAsync(ItemMasterUpsertDto dto);
        Task UpdateAsync(ItemMasterUpsertDto dto);
        Task DeleteAsync(int id);
        Task<IEnumerable<ItemStockDto?>> getStockByItemId(int itemId);
        Task<IEnumerable<ItemPriceDto?>> getPriceByItemId(int itemId);
        Task<IEnumerable<ItemMasterAuditDTO?>> getAuditByItemId(int itemId);
    }
}