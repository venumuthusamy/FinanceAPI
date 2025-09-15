using FinanceApi.Data;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;
using FinanceApi.Interfaces;

namespace FinanceApi.Repositories
{
    public class RegionRepository : IRegionRepository
    {
        private readonly ApplicationDbContext _context;

        public RegionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Region>> GetAllAsync()
        {
            return await _context.Region.Where(c => c.IsActive).OrderBy(c => c.Id).ToListAsync();
        }

        public async Task<Region?> GetByIdAsync(int id)
        {
            return await _context.Region.FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        }

        public async Task<Region> CreateAsync(Region region)
        {
            try
            {
                region.CreatedBy = "System";
                region.CreatedDate = DateTime.UtcNow;
                region.IsActive = true;
                _context.Region.Add(region);
                await _context.SaveChangesAsync();
                return region;
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

        public async Task<Region?> UpdateAsync(int id, Region updatedRegion)
        {
            try
            {
                var existingRegion = await _context.Region.FirstOrDefaultAsync(s => s.Id == id);
                if (existingRegion == null) return null;

                // Manually update only scalar properties (excluding Id)
                existingRegion.RegionName = updatedRegion.RegionName;
                existingRegion.UpdatedBy = "System";
                existingRegion.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return existingRegion;
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
            var region = await _context.Region.FirstOrDefaultAsync(s => s.Id == id);
            if (region == null) return false;

            region.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
