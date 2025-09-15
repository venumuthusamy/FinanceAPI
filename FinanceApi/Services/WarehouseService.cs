using FinanceApi.Interfaces;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class WarehouseService : IWarehouseService
    {
        private readonly IWarehouseRepository _repository;

        public WarehouseService(IWarehouseRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<WarehouseDto>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<WarehouseDto?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<Warehouse> CreateAsync(Warehouse warehouse)
        {
            return await _repository.CreateAsync(warehouse);
        }

        public async Task<Warehouse?> UpdateAsync(int id, Warehouse warehouse)
        {
            return await _repository.UpdateAsync(id, warehouse);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }
    }
}
