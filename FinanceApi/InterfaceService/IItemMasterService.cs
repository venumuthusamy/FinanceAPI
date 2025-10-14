using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.InterfaceService
{
    public interface IItemMasterService
    {
        Task<IEnumerable<ItemMasterDTO>> GetAllAsync();
        Task<ItemMasterDTO?> GetByIdAsync(int id);
        Task<int> CreateAsync(ItemMaster item);
        Task UpdateAsync(ItemMaster item);
        Task DeleteAsync(int id);
    }
}
