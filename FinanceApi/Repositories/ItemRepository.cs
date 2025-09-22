using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Repositories
{
    public class ItemRepository : IItemRepository
    {
        private readonly ApplicationDbContext _context;

        public ItemRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ItemDto>> GetAllAsync()
        {
            var result = await _context.Item
                .Include(c => c.Uom)
                .Include(c => c.ChartOfAccount)
                .Where(c => c.IsActive)
                .OrderBy(c => c.Id)
                .Select(c => new ItemDto
                {
                    Id = c.Id,
                    ItemCode = c.ItemCode,
                    ItemName = c.ItemName,
                    UomName = c.Uom != null ? c.Uom.Name : string.Empty,
                    UomId = c.UomId,
                    BudgetLineId = c.BudgetLineId,
                    CreatedBy = c.CreatedBy,
                    CreatedDate = c.CreatedDate,
                    UpdatedBy = c.UpdatedBy,
                    UpdatedDate = c.UpdatedDate,
                    IsActive = c.IsActive
                })
                .ToListAsync();

            return result;
        }



        public async Task<ItemDto?> GetByIdAsync(int id)
        {
            return await _context.Item
                .Include(c => c.Uom)
                .Include(c => c.ChartOfAccount)
                .Where(c => c.Id == id)
                .Where(c => c.IsActive)
                .Select(c => new ItemDto
                {
                    Id = c.Id,
                    ItemCode = c.ItemCode,
                    ItemName = c.ItemName,
                    UomName = c.Uom != null ? c.Uom.Name : string.Empty,
                    UomId = c.UomId,
                    BudgetLineId = c.BudgetLineId,
                    CreatedBy = c.CreatedBy,
                    CreatedDate = c.CreatedDate,
                    UpdatedBy = c.UpdatedBy,
                    UpdatedDate = c.UpdatedDate,
                    IsActive = c.IsActive
                })
                .FirstOrDefaultAsync();
        }

        public async Task<Item> CreateAsync(Item item)
        {
            try
            {
                item.CreatedBy = "System";
                item.CreatedDate = DateTime.UtcNow;
                item.IsActive = true;
                _context.Item.Add(item);
                await _context.SaveChangesAsync();
                return item;
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

        public async Task<Item?> UpdateAsync(int id, Item updatedItem)
        {
            try
            {
                var existingItem = await _context.Item.FirstOrDefaultAsync(s => s.Id == id);
                if (existingItem == null) return null;

                // Manually update only scalar properties (excluding Id)
                existingItem.ItemCode = updatedItem.ItemCode;
                existingItem.ItemName = updatedItem.ItemName;
                existingItem.UomId = updatedItem.UomId;
                existingItem.BudgetLineId = updatedItem.BudgetLineId;
                existingItem.UpdatedBy = "System";
                existingItem.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return existingItem;
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
            var item = await _context.Item.FirstOrDefaultAsync(s => s.Id == id);
            if (item == null) return false;

            item.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
