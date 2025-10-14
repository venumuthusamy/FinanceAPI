using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IItemMasterRepository
    {
        Task<IEnumerable<ItemMasterDTO>> GetAllAsync();
        Task<ItemMasterDTO?> GetByIdAsync(int id);
        Task<int> CreateAsync(ItemMaster item);      // Item is your EF/POCO model for table Items
        Task UpdateAsync(ItemMaster item);
        Task DeactivateAsync(int id);
    }
}
