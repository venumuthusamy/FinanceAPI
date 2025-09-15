using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IWarehouseService
    {
        Task<List<WarehouseDto>> GetAllAsync();
        Task<WarehouseDto?> GetByIdAsync(int id);
        Task<Warehouse> CreateAsync(Warehouse warehouse);
        Task<Warehouse?> UpdateAsync(int id, Warehouse warehouse);
        Task<bool> DeleteAsync(int id);
    }
}
