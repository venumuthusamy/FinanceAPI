using FinanceApi.ModelDTO;

namespace Interfaces
{
    public interface IItemMasterRepository
    {
        Task<IEnumerable<ItemMasterDTO>> GetAllAsync();
        Task<ItemMasterDTO?> GetByIdAsync(int id);
        Task<long> CreateAsync(ItemMasterUpsertDto dto);
        Task UpdateAsync(ItemMasterUpsertDto dto);
        Task DeactivateAsync(int id);
    }
}