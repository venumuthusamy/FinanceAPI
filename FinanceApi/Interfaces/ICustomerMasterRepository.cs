using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface ICustomerMasterRepository
    {
        Task<IEnumerable<CustomerMasterDTO>> GetAllAsync();
        Task<CustomerMasterDTO> GetByIdAsync(int id);
        Task<int> CreateAsync(CustomerMaster customerMaster);
        Task UpdateAsync(CustomerMaster customerMaster);
        Task DeactivateAsync(int id);
    }
}
