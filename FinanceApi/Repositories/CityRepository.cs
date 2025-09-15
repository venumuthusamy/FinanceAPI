using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Repositories
{
    public class CityRepository : ICityRepository
    {
        private readonly ApplicationDbContext _context;

        public CityRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<CityDto>> GetAllAsync()
        {
            var result = await _context.City
                .Include(c => c.State)
                .Include(c => c.Country)
                .Where(c => c.IsActive)
                .OrderBy(c => c.Id)
                .Select(c => new CityDto
                {
                    Id = c.Id,
                    CityId = c.Id,
                    CityName = c.CityName,
                    StateId = c.StateId,
                    StateName = c.State != null ? c.State.StateName : string.Empty,
                    CountryId = c.CountryId,
                    CountryName = c.Country != null ? c.Country.CountryName : string.Empty,
                    CreatedBy = c.CreatedBy,
                    CreatedDate = c.CreatedDate,
                    UpdatedBy = c.UpdatedBy,
                    UpdatedDate = c.UpdatedDate,
                    IsActive = c.IsActive
                })
                .ToListAsync();

            return result;
        }



        public async Task<CityDto?> GetByIdAsync(int id)
        {
            return await _context.City
                .Include(c => c.State)
                .Include(c => c.Country)
                .Where(c => c.Id == id)
                .Where(c => c.IsActive)
                .Select(c => new CityDto
                {
                    Id = c.Id,
                    CityId = c.Id,
                    CityName = c.CityName,
                    StateId = c.StateId,
                    StateName = c.State != null ? c.State.StateName : string.Empty,
                    CountryId = c.CountryId,
                    CountryName = c.Country != null ? c.Country.CountryName : string.Empty,
                    CreatedBy = c.CreatedBy,
                    CreatedDate = c.CreatedDate,
                    UpdatedBy = c.UpdatedBy,
                    UpdatedDate = c.UpdatedDate,
                    IsActive = c.IsActive
                })
                .FirstOrDefaultAsync();
        }

        public async Task<City> CreateAsync(City city)
        {
            try
            {
                city.CreatedBy = "System";
                city.CreatedDate = DateTime.UtcNow;
                city.IsActive = true;
                _context.City.Add(city);
                await _context.SaveChangesAsync();
                return city;
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

        public async Task<City?> UpdateAsync(int id, City updatedCity)
        {
            try
            {
                var existingCity = await _context.City.FirstOrDefaultAsync(s => s.Id == id);
                if (existingCity == null) return null;

                // Manually update only scalar properties (excluding Id)
                existingCity.CityName = updatedCity.CityName;
                existingCity.StateId = updatedCity.StateId;
                existingCity.CountryId = updatedCity.CountryId;
                existingCity.UpdatedBy = "System";
                existingCity.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return existingCity;
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
            var city = await _context.City.FirstOrDefaultAsync(s => s.Id == id);
            if (city == null) return false;

            city.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
