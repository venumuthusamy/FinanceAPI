using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Repositories
{
    public class CurrencyRepository : ICurrencyRepository
    {
        private readonly ApplicationDbContext _context;

        public CurrencyRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET ALL active currencies
        public async Task<IEnumerable<CurrencyDTO>> GetAllAsync()
        {
            return await _context.Currencies
                .Where(c => c.IsActive == true)
                .Select(c => new CurrencyDTO
                {
                    Id = c.Id,
                    CurrencyName = c.CurrencyName,
                    Description = c.Description,
                    CreatedBy = c.CreatedBy,
                    CreatedDate = c.CreatedDate,
                    UpdatedBy = c.UpdatedBy,
                    UpdatedDate = c.UpdatedDate,
                    IsActive = c.IsActive
                })
                .ToListAsync();
        }

        // GET BY ID (only active)
        public async Task<CurrencyDTO?> GetByIdAsync(int id)
        {
            var c = await _context.Currencies
                .Where(x => x.Id == id && x.IsActive == true)
                .FirstOrDefaultAsync();

            if (c == null) return null;

            return new CurrencyDTO
            {
                Id = c.Id,
                CurrencyName = c.CurrencyName,
                Description = c.Description,
                CreatedBy = c.CreatedBy,
                CreatedDate = c.CreatedDate,
                UpdatedBy = c.UpdatedBy,
                UpdatedDate = c.UpdatedDate,
                IsActive = c.IsActive
            };
        }

        // CREATE
        public async Task<Currency> AddAsync(Currency currency)
        {
            currency.CreatedDate = DateTime.UtcNow;
            _context.Currencies.Add(currency);
            await _context.SaveChangesAsync();
            return currency;
        }

        // UPDATE
        public async Task<bool> UpdateAsync(Currency currency)
        {
            var existing = await _context.Currencies
                .Where(c => c.Id == currency.Id && c.IsActive == true)
                .FirstOrDefaultAsync();

            if (existing == null) return false;

           
            existing.CurrencyName = currency.CurrencyName;
            existing.UpdatedBy = currency.UpdatedBy;
            existing.UpdatedDate = DateTime.UtcNow;
            existing.IsActive = currency.IsActive;

            _context.Currencies.Update(existing);
            await _context.SaveChangesAsync();
            return true;
        }

        // SOFT DELETE
        public async Task<bool> DeleteAsync(int id)
        {
            var existing = await _context.Currencies.FindAsync(id);
            if (existing == null) return false;

            existing.IsActive = false;
            existing.UpdatedDate = DateTime.UtcNow;

            _context.Currencies.Update(existing);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
