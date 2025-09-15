using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface ICustomerGroupsService
    {
        Task<List<CustomerGroups>> GetAllAsync();
        Task<CustomerGroups?> GetByIdAsync(int id);
        Task<CustomerGroups> CreateAsync(CustomerGroups customerGroups);
        Task<CustomerGroups?> UpdateAsync(int id, CustomerGroups customerGroups);
        Task<bool> DeleteAsync(int id);
    }
}
