using FinanceApi.Models;
using FinanceApi.Data;
using Microsoft.EntityFrameworkCore;
using FinanceApi.Interfaces;


namespace FinanceApi.Repositories
{
    public class CountryRepository : ICountryRepository
    {
        private readonly ApplicationDbContext _context;

        public CountryRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Country>> GetAllAsync()
        {
            return await _context.Country.Where(c => c.IsActive).OrderBy(c => c.Id).ToListAsync();
        }

        public async Task<Country?> GetByIdAsync(int id)
        {
            return await _context.Country.FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        }

        public async Task<Country> CreateAsync(Country country)
        {
            try
            {
                country.CreatedBy = "System";
                country.CreatedDate = DateTime.UtcNow;
                country.IsActive = true;
                _context.Country.Add(country);
                await _context.SaveChangesAsync();
                return country;
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

        public async Task<Country?> UpdateAsync(int id, Country updatedCountry)
        {
            try
            {
                var existingCountry = await _context.Country.FirstOrDefaultAsync(s => s.Id == id);
                if (existingCountry == null) return null;

                // Manually update only scalar properties (excluding Id)
                existingCountry.CountryName = updatedCountry.CountryName;
                existingCountry.UpdatedBy = "System";
                existingCountry.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return existingCountry;
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
            var country = await _context.Country.FirstOrDefaultAsync(s => s.Id == id);
            if (country == null) return false;

            country.IsActive = false;
            
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
