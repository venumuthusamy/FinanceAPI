using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Repositories
{
    public class LocationRepository : ILocationRepository
    {
        private readonly ApplicationDbContext _context;

        public LocationRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<LocationDto>> GetAllAsync()
        {
            var result = await _context.Location
                .Include(c => c.City)
                .Include(c => c.State)
                .Include(c => c.Country)
                .Where(c => c.IsActive)
                .OrderBy(c => c.Id)
                .Select(c => new LocationDto
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
                    Latitude = c.Latitude,
                    Longitude = c.Longitude,             
                    CreatedBy = c.CreatedBy,
                    CreatedDate = c.CreatedDate,
                    UpdatedBy = c.UpdatedBy,
                    UpdatedDate = c.UpdatedDate,
                    IsActive = c.IsActive

                })
                .ToListAsync();
            return result;
        }

        public async Task<LocationDto?> GetByIdAsync(int id)
        {
            var result = await _context.Location
                .Include(c => c.City)
                .Include(c => c.State)
                .Include(c => c.Country)              
                .Where(c => c.Id == id)
                .Where(c => c.IsActive)
                .Select(c => new LocationDto
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
                    Latitude = c.Latitude,
                    Longitude = c.Longitude,
                    CreatedBy = c.CreatedBy,
                    CreatedDate = c.CreatedDate,
                    UpdatedBy = c.UpdatedBy,
                    UpdatedDate = c.UpdatedDate,
                    IsActive = c.IsActive

                })
                .FirstOrDefaultAsync();
            return result;
        }

        public async Task<Location> CreateAsync(Location location)
        {
            try
            {
                location.CreatedBy = "System";
                location.CreatedDate = DateTime.UtcNow;
                location.IsActive = true;
                _context.Location.Add(location);
                await _context.SaveChangesAsync();
                return location;
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

        public async Task<Location?> UpdateAsync(int id, Location updatedLocation)
        {
            try
            {
                var existingLocation = await _context.Location.FirstOrDefaultAsync(s => s.Id == id);
                if (existingLocation == null) return null;

                // Manually update only scalar properties (excluding Id)
                existingLocation.Name = updatedLocation.Name;
                existingLocation.CityId = updatedLocation.CityId;
                existingLocation.StateId = updatedLocation.StateId;
                existingLocation.CountryId = updatedLocation.CountryId;
                existingLocation.Address = updatedLocation.Address;
                existingLocation.Latitude = updatedLocation.Latitude;
                existingLocation.Longitude = updatedLocation.Longitude;
                existingLocation.UpdatedBy = "System";
                existingLocation.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return existingLocation;
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
            var location = await _context.Location.FirstOrDefaultAsync(s => s.Id == id);
            if (location == null) return false;

            location.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
