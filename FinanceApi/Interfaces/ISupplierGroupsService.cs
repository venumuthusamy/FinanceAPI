using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface ISupplierGroupsService
    {
        Task<List<SupplierGroups>> GetAllAsync();
        Task<SupplierGroups?> GetByIdAsync(int id);
        Task<SupplierGroups> CreateAsync(SupplierGroups supplierGroups);
        Task<SupplierGroups?> UpdateAsync(int id, SupplierGroups supplierGroups);
        Task<bool> DeleteAsync(int id);
    }
}
