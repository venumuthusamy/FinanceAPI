using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class ItemMasterService : IItemMasterService
    {
        private readonly IItemMasterRepository _repo;
        public ItemMasterService(IItemMasterRepository repo) { _repo = repo; }

        public Task<IEnumerable<ItemMasterDTO>> GetAllAsync() => _repo.GetAllAsync();
        public Task<ItemMasterDTO?> GetByIdAsync(int id) => _repo.GetByIdAsync(id);
        public Task<int> CreateAsync(ItemMaster item) => _repo.CreateAsync(item);
        public Task UpdateAsync(ItemMaster item) => _repo.UpdateAsync(item);
        public Task DeleteAsync(int id) => _repo.DeactivateAsync(id);
    }
}
