using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.InterfaceService
{
    public interface ISuppliersService
    {
        Task<IEnumerable<SuppliersDTO>> GetAllAsync();
        Task<SuppliersDTO?> GetByIdAsync(int id);
        Task<Suppliers> CreateAsync(Suppliers supplier);
        Task<bool> UpdateAsync(int id, Suppliers supplier);
        Task<bool> DeleteAsync(int id);
    }
}
