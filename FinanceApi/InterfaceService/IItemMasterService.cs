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
    }
}