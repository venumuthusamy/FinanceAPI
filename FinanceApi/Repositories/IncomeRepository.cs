using FinanceApi.Models;
using FinanceApi.Data;
using Microsoft.EntityFrameworkCore;
using FinanceApi.Interfaces;


namespace FinanceApi.Repositories
{
    public class IncomeRepository : IIncomeRepository
    {
        private readonly ApplicationDbContext _context;

        public IncomeRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Income>> GetAllAsync()
        {
            return await _context.Income.Where(c => c.IsActive).OrderBy(c => c.Id).ToListAsync();
        }

        public async Task<Income?> GetByIdAsync(int id)
        {
            return await _context.Income.FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        }

        public async Task<Income> CreateAsync(Income income)
        {
            try
            {
                income.CreatedBy = "System";
                income.CreatedDate = DateTime.UtcNow;
                income.IsActive = true;
                _context.Income.Add(income);
                await _context.SaveChangesAsync();
                return income;
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

        public async Task<Income?> UpdateAsync(int id, Income updatedIncome)
        {
            try
            {
                var existingIncome = await _context.Income.FirstOrDefaultAsync(s => s.Id == id);
                if (existingIncome == null) return null;

                // Manually update only scalar properties (excluding Id)
                existingIncome.Name = updatedIncome.Name;
                existingIncome.Description = updatedIncome.Description;
                existingIncome.UpdatedBy = "System";
                existingIncome.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return existingIncome;
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
            var income = await _context.Income.FirstOrDefaultAsync(s => s.Id == id);
            if (income == null) return false;

            income.IsActive = false;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
