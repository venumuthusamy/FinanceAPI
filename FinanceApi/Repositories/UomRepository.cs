using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Repositories
{
    public class UomRepository : IUomRepository
    {
        private readonly ApplicationDbContext _context;

        public UomRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Uom>> GetAllAsync()
        {
            return await _context.Uom.Where(c => c.IsActive).OrderBy(c => c.Id).ToListAsync();
        }

        public async Task<Uom?> GetByIdAsync(int id)
        {
            return await _context.Uom.FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        }

        public async Task<Uom> CreateAsync(Uom uom)
        {
            try
            {
                uom.CreatedBy = "System";
                uom.CreatedDate = DateTime.UtcNow;
                uom.IsActive = true;
                _context.Uom.Add(uom);
                await _context.SaveChangesAsync();
                return uom;
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

        public async Task<Uom?> UpdateAsync(int id, Uom updatedUom)
        {
            try
            {
                var existingUom = await _context.Uom.FirstOrDefaultAsync(s => s.Id == id);
                if (existingUom == null) return null;

                // Manually update only scalar properties (excluding Id)
                existingUom.Name = updatedUom.Name;
                existingUom.Description = updatedUom.Description;
                existingUom.UpdatedBy = "System";
                existingUom.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return existingUom;
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
            var uom = await _context.Uom.FirstOrDefaultAsync(s => s.Id == id);
            if (uom == null) return false;

            uom.IsActive = false;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
