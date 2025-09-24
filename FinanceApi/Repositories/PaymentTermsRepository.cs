using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Repositories
{
    public class PaymentTermsRepository : IPaymentTermsRepository
    {
        private readonly ApplicationDbContext _context;

        public PaymentTermsRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaymentTerms> AddAsync(PaymentTerms paymentTerms)
        {
            paymentTerms.CreatedDate = DateTime.UtcNow;
            _context.PaymentTerms.Add(paymentTerms);
            await _context.SaveChangesAsync();
            return paymentTerms;
        }


        public async Task<bool> DeleteAsync(int id)
        {
            var existing = await _context.PaymentTerms.FindAsync(id);
            if (existing == null) return false;

            // Soft delete: mark as inactive
            existing.IsActive = false;
            existing.UpdatedDate = DateTime.UtcNow;

            _context.PaymentTerms.Update(existing);
            await _context.SaveChangesAsync();
            return true;
        }


        public async Task<IEnumerable<PaymentTermsDTO>> GetAllAsync()
        {
            return await _context.PaymentTerms
                .Where(p => p.IsActive == true)  // only active records
                .Select(p => new PaymentTermsDTO
                {
                    Id = p.Id,
                    PaymentTermsName = p.PaymentTermsName,
                    Description = p.Description,
                    CreatedBy = p.CreatedBy,
                    CreatedDate = p.CreatedDate,
                    UpdatedBy = p.UpdatedBy,
                    UpdatedDate = p.UpdatedDate,
                    IsActive = p.IsActive
                })
                .ToListAsync();
        }

        public async Task<PaymentTermsDTO?> GetByIdAsync(int id)
        {
            var p = await _context.PaymentTerms
                .Where(pt => pt.Id == id && pt.IsActive == true)  // only active
                .FirstOrDefaultAsync();

            if (p == null) return null;

            return new PaymentTermsDTO
            {
                Id = p.Id,
                PaymentTermsName = p.PaymentTermsName,
                Description = p.Description,
                CreatedBy = p.CreatedBy,
                CreatedDate = p.CreatedDate,
                UpdatedBy = p.UpdatedBy,
                UpdatedDate = p.UpdatedDate,
                IsActive = p.IsActive
            };
        }

        public async Task<bool> UpdateAsync(PaymentTerms paymentTerms)
        {
            var existing = await _context.PaymentTerms.FindAsync(paymentTerms.Id);
            if (existing == null) return false;

            existing.PaymentTermsName = paymentTerms.PaymentTermsName;
            existing.Description = paymentTerms.Description;
            existing.UpdatedBy = paymentTerms.UpdatedBy;
            existing.UpdatedDate = DateTime.UtcNow;
            existing.IsActive = paymentTerms.IsActive;

            _context.PaymentTerms.Update(existing);
            await _context.SaveChangesAsync();
            return true;
        }

    }
}
