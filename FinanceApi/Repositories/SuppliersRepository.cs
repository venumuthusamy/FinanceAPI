using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Repositories
{
    public class SuppliersRepository : ISuppliersRepository
    {
        private readonly ApplicationDbContext _context;

        public SuppliersRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        // Add new supplier
        public async Task<Suppliers> AddAsync(Suppliers supplier)
        {
            supplier.CreatedDate = DateTime.UtcNow;
            supplier.IsActive = true;

            _context.suppliers.Add(supplier);
            await _context.SaveChangesAsync();
            return supplier;
        }

        // Soft delete
        public async Task<bool> DeleteAsync(int id)
        {
            var existing = await _context.suppliers.FindAsync(id);
            if (existing == null) return false;

            existing.IsActive = false;
            existing.UpdatedDate = DateTime.UtcNow;

            _context.suppliers.Update(existing);
            await _context.SaveChangesAsync();
            return true;
        }

        // Get all active suppliers
        public async Task<IEnumerable<SuppliersDTO>> GetAllAsync()
        {
            return await _context.suppliers
                .Where(s => s.IsActive == true)  // filter active suppliers
                .Select(s => new SuppliersDTO
                {
                    Id = s.Id,
                    Name = s.Name,
                    Code = s.Code,
                    StatusId = s.StatusId,
                    LeadTime = s.LeadTime,
                    TermsId = s.TermsId,
                    CurrencyId = s.CurrencyId,
                    TaxReg = s.TaxReg,
                    IncotermsId = s.IncotermsId,
                    Contact = s.Contact,
                    Email = s.Email,
                    Phone = s.Phone,
                    Address = s.Address,
                    BankName = s.BankName,
                    BankAcc = s.BankAcc,
                    BankSwift = s.BankSwift,
                    BankBranch = s.BankBranch,
                    CreatedBy = s.CreatedBy,
                    CreatedDate = s.CreatedDate,
                    UpdatedBy = s.UpdatedBy,
                    UpdatedDate = s.UpdatedDate,
                    IsActive = s.IsActive
                })
                .ToListAsync();
        }



        // Get supplier by id
        public async Task<SuppliersDTO?> GetByIdAsync(int id)
        {
            var s = await _context.suppliers
                .Where(s => s.Id == id && s.IsActive == true)
                .FirstOrDefaultAsync();

            if (s == null) return null;

            return new SuppliersDTO
            {
                Id = s.Id,
                Name = s.Name,
                Code = s.Code,
                StatusId = s.StatusId,
                LeadTime = s.LeadTime,
                TermsId = s.TermsId,
                CurrencyId = s.CurrencyId,
                TaxReg = s.TaxReg,
                IncotermsId = s.IncotermsId,
                Contact = s.Contact,
                Email = s.Email,
                Phone = s.Phone,
                Address = s.Address,
                BankName = s.BankName,
                BankAcc = s.BankAcc,
                BankSwift = s.BankSwift,
                BankBranch = s.BankBranch,
                CreatedBy = s.CreatedBy,
                CreatedDate = s.CreatedDate,
                UpdatedBy = s.UpdatedBy,
                UpdatedDate = s.UpdatedDate,
                IsActive = s.IsActive
            };
        }



        // Update supplier
        public async Task<bool> UpdateAsync(Suppliers supplier)
        {
            var existing = await _context.suppliers.FindAsync(supplier.Id);
            if (existing == null) return false;

            existing.Name = supplier.Name;
            existing.Code = supplier.Code;
            existing.StatusId = supplier.StatusId;
            existing.LeadTime = supplier.LeadTime;
            existing.TermsId = supplier.TermsId;
            existing.CurrencyId = supplier.CurrencyId;
            existing.TaxReg = supplier.TaxReg;
            existing.IncotermsId = supplier.IncotermsId;
            existing.Contact = supplier.Contact;
            existing.Email = supplier.Email;
            existing.Phone = supplier.Phone;
            existing.Address = supplier.Address;

            // Flat bank fields
            existing.BankName = supplier.BankName;
            existing.BankAcc = supplier.BankAcc;
            existing.BankSwift = supplier.BankSwift;
            existing.BankBranch = supplier.BankBranch;

            existing.UpdatedBy = supplier.UpdatedBy;
            existing.UpdatedDate = DateTime.UtcNow;
            existing.IsActive = supplier.IsActive;

            _context.suppliers.Update(existing);
            await _context.SaveChangesAsync();
            return true;
        }


    }
}
