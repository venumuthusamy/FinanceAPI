using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IDepartmentService
    {
        Task<List<Department>> GetAllAsync();
        Task<Department?> GetByIdAsync(int id);
        Task<Department> CreateAsync(Department department);
        Task<Department?> UpdateAsync(int id, Department department);
        Task<bool> DeleteAsync(int id);
    }
}
