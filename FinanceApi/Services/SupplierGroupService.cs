using FinanceApi.Interfaces;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class SupplierGroupService : ISupplierGroupsService
    {
        private readonly ISupplierGroupsRepository _repository;

        public SupplierGroupService(ISupplierGroupsRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<SupplierGroups>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<SupplierGroups?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<SupplierGroups> CreateAsync(SupplierGroups supplierGroups)
        {

            return await _repository.CreateAsync(supplierGroups);
        }

        public async Task<SupplierGroups?> UpdateAsync(int id, SupplierGroups supplierGroups)
        {
            return await _repository.UpdateAsync(id, supplierGroups);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }
    }
}
