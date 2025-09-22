using FinanceApi.Models;
using FinanceApi.Data;
using Microsoft.EntityFrameworkCore;
using FinanceApi.Interfaces;

namespace FinanceApi.Repositories
{
    public class DepartmentRepository : IDepartmentRepository
    {
        private readonly ApplicationDbContext _context;

        public DepartmentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Department>> GetAllAsync()
        {
            return await _context.Department.Where(c => c.IsActive).OrderBy(c => c.Id).ToListAsync();
        }

        public async Task<Department?> GetByIdAsync(int id)
        {
            return await _context.Department.FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        }

        public async Task<Department> CreateAsync(Department department)
        {
            try
            {
                department.CreatedBy = "System";
                department.CreatedDate = DateTime.UtcNow;
                department.IsActive = true;
                _context.Department.Add(department);
                await _context.SaveChangesAsync();
                return department;
            }
            catch (DbUpdateException dbEx)
            {
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                throw new Exception($"Error: {innerMessage}", dbEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

        public async Task<Department?> UpdateAsync(int id, Department updatedDepartment)
        {
            try
            {
                var existingDepartment = await _context.Department.FirstOrDefaultAsync(s => s.Id == id);
                if (existingDepartment == null) return null;

                // Manually update only scalar properties (excluding Id)
                existingDepartment.DepartmentCode = updatedDepartment.DepartmentCode;
                existingDepartment.DepartmentName = updatedDepartment.DepartmentName;
                existingDepartment.UpdatedBy = "System";
                existingDepartment.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return existingDepartment;
            }

            catch (DbUpdateException dbEx)
            {
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                throw new Exception($"Error: {innerMessage}", dbEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }


        }


        public async Task<bool> DeleteAsync(int id)
        {
            var department = await _context.Department.FirstOrDefaultAsync(s => s.Id == id);
            if (department == null) return false;

            department.IsActive = false;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}

