using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IItemRepository
    {
        Task<List<ItemDto>> GetAllAsync();
        Task<ItemDto?> GetByIdAsync(int id);
        Task<Item> CreateAsync(Item item);
        Task<Item?> UpdateAsync(int id, Item item);
        Task<bool> DeleteAsync(int id);
    }
}
