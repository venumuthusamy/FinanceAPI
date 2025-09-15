using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;


namespace FinanceApi.Repositories
{
    public class WarehouseRepository : IWarehouseRepository
    {
        private readonly ApplicationDbContext _context;

        public WarehouseRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<WarehouseDto>> GetAllAsync()
        {
            var result = await _context.Warehouse
                .Include(c => c.City)
                .Include(c => c.State)
                .Include(c => c.Country)
                .Where(c => c.IsActive)
                .OrderBy(c => c.Id)
                .Select(c => new WarehouseDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    CityId = c.CityId,
                    CityName = c.City != null ? c.City.CityName : string.Empty,
                    StateId = c.StateId,
                    StateName = c.State != null ? c.State.StateName : string.Empty,
                    CountryId = c.CountryId,
                    CountryName = c.Country != null ? c.Country.CountryName : string.Empty,
                    Address = c.Address,
                    Description = c.Description,
                    Phone = c.Phone,
                    CreatedBy = c.CreatedBy,
                    CreatedDate = c.CreatedDate,
                    UpdatedBy = c.UpdatedBy,
                    UpdatedDate = c.UpdatedDate,
                    IsActive = c.IsActive

                })
                .ToListAsync();
            return result;
        }

        public async Task<WarehouseDto?> GetByIdAsync(int id)
        {
            var result = await _context.Warehouse
                .Include(c => c.City)
                .Include(c => c.State)
                .Include(c => c.Country)
                .Where(c => c.Id == id)
                .Where(c => c.IsActive)
                .Select(c => new WarehouseDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    CityId = c.CityId,
                    CityName = c.City != null ? c.City.CityName : string.Empty,
                    StateId = c.StateId,
                    StateName = c.State != null ? c.State.StateName : string.Empty,
                    CountryId = c.CountryId,
                    CountryName = c.Country != null ? c.Country.CountryName : string.Empty,
                    Address = c.Address,
                    Description = c.Description,
                    Phone = c.Phone,
                    CreatedBy = c.CreatedBy,
                    CreatedDate = c.CreatedDate,
                    UpdatedBy = c.UpdatedBy,
                    UpdatedDate = c.UpdatedDate,
                    IsActive = c.IsActive

                })
                .FirstOrDefaultAsync();
            return result;
        }

        public async Task<Warehouse> CreateAsync(Warehouse warehouse)
        {
            try
            {
                warehouse.CreatedBy = "System";
                warehouse.CreatedDate = DateTime.UtcNow;
                warehouse.IsActive = true;
                _context.Warehouse.Add(warehouse);
                await _context.SaveChangesAsync();
                return warehouse;
            }
            catch (DbUpdateException dbEx)
            {
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                throw new Exception($"Error saving supplier: {innerMessage}", dbEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General error: {ex.Message}");
                throw;
            }
        }

        public async Task<Warehouse?> UpdateAsync(int id, Warehouse updatedWarehouse)
        {
            try
            {
                var existingWarehouse = await _context.Warehouse.FirstOrDefaultAsync(s => s.Id == id);
                if (existingWarehouse == null) return null;

                // Manually update only scalar properties (excluding Id)
                existingWarehouse.Name = updatedWarehouse.Name;
                existingWarehouse.CityId = updatedWarehouse.CityId;
                existingWarehouse.StateId = updatedWarehouse.StateId;
                existingWarehouse.CountryId = updatedWarehouse.CountryId;
                existingWarehouse.Address = updatedWarehouse.Address;
                existingWarehouse.Description = updatedWarehouse.Description;
                existingWarehouse.Phone = updatedWarehouse.Phone;
                existingWarehouse.UpdatedBy = "System";
                existingWarehouse.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return existingWarehouse;
            }

            catch (DbUpdateException dbEx)
            {
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                throw new Exception($"Error saving supplier: {innerMessage}", dbEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General error: {ex.Message}");
                throw;
            }
        }


        public async Task<bool> DeleteAsync(int id)
        {
            var warehouse = await _context.Warehouse.FirstOrDefaultAsync(s => s.Id == id);
            if (warehouse == null) return false;

            warehouse.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
