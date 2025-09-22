using FinanceApi.Interfaces;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class ItemService : IItemService
    {
        private readonly IItemRepository _repository;

        public ItemService(IItemRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<ItemDto>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<ItemDto?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<Item> CreateAsync(Item item)
        {

            return await _repository.CreateAsync(item);
        }

        public async Task<Item?> UpdateAsync(int id, Item item)
        {
            return await _repository.UpdateAsync(id, item);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }
    }
}
