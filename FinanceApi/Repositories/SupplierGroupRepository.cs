using FinanceApi.Data;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;
using FinanceApi.Interfaces;


namespace FinanceApi.Repositories
{
    public class SupplierGroupRepository : ISupplierGroupsRepository
    {
        private readonly ApplicationDbContext _context;

        public SupplierGroupRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<SupplierGroups>> GetAllAsync()
        {
            return await _context.SupplierGroups.Where(c => c.IsActive).OrderBy(c => c.Id).ToListAsync();
        }

        public async Task<SupplierGroups?> GetByIdAsync(int id)
        {
            return await _context.SupplierGroups.FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        }

        public async Task<SupplierGroups> CreateAsync(SupplierGroups supplierGroups)
        {
            try
            {
                supplierGroups.CreatedBy = "System";
                supplierGroups.CreatedDate = DateTime.UtcNow;
                supplierGroups.IsActive = true;
                _context.SupplierGroups.Add(supplierGroups);
                await _context.SaveChangesAsync();
                return supplierGroups;
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

        public async Task<SupplierGroups?> UpdateAsync(int id, SupplierGroups updatedSupplierGroups)
        {
            try
            {
                var existingSupplierGroups = await _context.SupplierGroups.FirstOrDefaultAsync(s => s.Id == id);
                if (existingSupplierGroups == null) return null;

                // Manually update only scalar properties (excluding Id)
                existingSupplierGroups.Name = updatedSupplierGroups.Name;
                existingSupplierGroups.Description = updatedSupplierGroups.Description;
                existingSupplierGroups.UpdatedBy = "System";
                existingSupplierGroups.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return existingSupplierGroups;
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
            var supplierGroups = await _context.SupplierGroups.FirstOrDefaultAsync(s => s.Id == id);
            if (supplierGroups == null) return false;

            supplierGroups.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
