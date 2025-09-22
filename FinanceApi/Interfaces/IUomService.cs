using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IUomService
    {
        Task<List<Uom>> GetAllAsync();
        Task<Uom?> GetByIdAsync(int id);
        Task<Uom> CreateAsync(Uom uom);
        Task<Uom?> UpdateAsync(int id, Uom uom);
        Task<bool> DeleteAsync(int id);
    }
}
