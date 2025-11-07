using FinanceApi.ModelDTO;
using FinanceApi.Models;
using System.Threading.Tasks;

namespace FinanceApi.InterfaceService
{
    public interface ICustomerMasterService
    {
        Task<IEnumerable<CustomerMasterDTO>> GetAllAsync();
        Task<int> CreateAsync(CustomerMaster customerMaster);
        Task<CustomerMasterDTO> GetById(int id);
        Task UpdateAsync(CustomerMaster customerMaster);
        Task DeleteAsync(int id);
    }
}
