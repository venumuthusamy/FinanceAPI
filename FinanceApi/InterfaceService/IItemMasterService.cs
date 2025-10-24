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
        Task<IEnumerable<ItemWarehouseStockDTO?>> getStockByItemId(int itemId);
        Task<IEnumerable<ItemWarehouseStockDTO?>> getPriceByItemId(int itemId);
        Task<IEnumerable<ItemWarehouseStockDTO?>> getAuditByItemId(int itemId);
        Task<BomSnapshot> GetBomSnapshot(int itemId);
    }
}