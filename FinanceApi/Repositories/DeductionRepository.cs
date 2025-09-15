using FinanceApi.Models;
using FinanceApi.Data;
using Microsoft.EntityFrameworkCore;
using FinanceApi.Interfaces;

namespace FinanceApi.Repositories
{
    public class DeductionRepository : IDeductionRepository
    {
        private readonly ApplicationDbContext _context;

        public DeductionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Deduction>> GetAllAsync()
        {
            return await _context.Deduction.Where(c => c.IsActive).OrderBy(c => c.Id).ToListAsync();
        }

        public async Task<Deduction?> GetByIdAsync(int id)
        {
            return await _context.Deduction.FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        }

        public async Task<Deduction> CreateAsync(Deduction deduction)
        {
            try
            {
                deduction.CreatedBy = "System";
                deduction.CreatedDate = DateTime.UtcNow;
                deduction.IsActive = true;

                _context.Deduction.Add(deduction);
                await _context.SaveChangesAsync();

                return deduction;
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

        public async Task<Deduction?> UpdateAsync(int id, Deduction updatedDeduction)
        {
            try
            {
                var existingDeduction = await _context.Deduction.FirstOrDefaultAsync(s => s.Id == id);
                if (existingDeduction == null) return null;

                // Manually update only scalar properties (excluding Id)
                existingDeduction.Name = updatedDeduction.Name;
                existingDeduction.Description = updatedDeduction.Description;
                existingDeduction.UpdatedBy = "System";
                existingDeduction.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return existingDeduction;
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
            var income = await _context.Deduction.FirstOrDefaultAsync(s => s.Id == id);
            if (income == null) return false;

            income.IsActive = false;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
