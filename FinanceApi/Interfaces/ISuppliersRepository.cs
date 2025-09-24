using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface ISuppliersRepository
    {
        Task<IEnumerable<SuppliersDTO>> GetAllAsync();
        Task<SuppliersDTO?> GetByIdAsync(int id);
        Task<Suppliers> AddAsync(Suppliers supplier);
        Task<bool> UpdateAsync(Suppliers supplier);
        Task<bool> DeleteAsync(int id);
    }
}
