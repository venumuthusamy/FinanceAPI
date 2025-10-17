using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Interfaces;
using InterfaceService;

namespace FinanceApi.Services
{
    public class ItemMasterService : IItemMasterService
    {
        private readonly IItemMasterRepository _repo;
        public ItemMasterService(IItemMasterRepository repo) => _repo = repo;

        public Task<IEnumerable<ItemMasterDTO>> GetAllAsync() => _repo.GetAllAsync();
        public Task<ItemMasterDTO?> GetByIdAsync(int id) => _repo.GetByIdAsync(id);
        public Task<long> CreateAsync(ItemMasterUpsertDto dto) => _repo.CreateAsync(dto);
        public Task UpdateAsync(ItemMasterUpsertDto dto) => _repo.UpdateAsync(dto);
        public Task DeleteAsync(int id) => _repo.DeactivateAsync(id);

        public Task<IEnumerable<ItemStockDto>> getStockByItemId(int itemId)
        {
           return _repo.GetWarehouseStockByItemAsync(itemId);
        }

        public Task<IEnumerable<ItemPriceDto?>> getPriceByItemId(int itemId)
        {
          return _repo.GetSupplierPricesByItemAsync(itemId);
        }

      
        public Task<IEnumerable<ItemMasterAuditDTO?>> getAuditByItemId(int itemId)
        {
            return _repo.GetAuditsByItemAsync(itemId);
        }
    }
}
