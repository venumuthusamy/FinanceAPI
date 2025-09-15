using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Repositories
{
    public class ChartOfAccountRepository : IChartOfAccountRepository
    {
        private readonly ApplicationDbContext _context;

        public ChartOfAccountRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<ChartOfAccount>> GetAllAsync()
        {
            return await _context.ChartOfAccount
                .Where(c => c.IsActive)
                .OrderBy(c => c.Id)
                .Select(s => new ChartOfAccount
                {
                    Id = s.Id,
                    HeadName = s.HeadName,
                    HeadCode = s.HeadCode,
                    HeadLevel = s.HeadLevel,
                    HeadType = s.HeadType,
                    HeadCodeName = s.HeadCodeName,
                    IsGl = s.IsGl,
                    IsTransaction = s.IsTransaction,
                    ParentHead = s.ParentHead,
                    PHeadName = s.PHeadName,
                    Balance = s.Balance,
                    OpeningBalance = s.OpeningBalance,
                    CreatedBy = s.CreatedBy,
                    CreatedDate = s.CreatedDate,
                    UpdatedBy = s.UpdatedBy,
                    UpdatedDate = s.UpdatedDate,
                    IsActive = s.IsActive
                })
                .ToListAsync();
        }


        public async Task<ChartOfAccount?> GetByIdAsync(int id)
        {
            return await _context.ChartOfAccount
                .Where(s => s.Id == id)
                .Where(c => c.IsActive)
                .Select(s => new ChartOfAccount
                {
                    Id = s.Id,
                    HeadName = s.HeadName,
                    HeadCode = s.HeadCode,
                    HeadLevel = s.HeadLevel,
                    HeadType = s.HeadType,
                    HeadCodeName = s.HeadCodeName,
                    IsGl = s.IsGl,
                    IsTransaction = s.IsTransaction,
                    ParentHead = s.ParentHead,
                    PHeadName = s.PHeadName,
                    Balance = s.Balance,
                    OpeningBalance = s.OpeningBalance,
                    CreatedBy = s.CreatedBy,
                    CreatedDate = s.CreatedDate,
                    UpdatedBy = s.UpdatedBy,
                    UpdatedDate = s.UpdatedDate,
                    IsActive = s.IsActive
                })
                .FirstOrDefaultAsync();
        }


        public async Task<ChartOfAccount> CreateAsync(ChartOfAccount coa)
        {
            try
            {
                coa.CreatedBy = "System";
                coa.CreatedDate = DateTime.UtcNow;
                coa.IsActive = true;
                _context.ChartOfAccount.Add(coa);
                await _context.SaveChangesAsync();
                return coa;

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

        public async Task<ChartOfAccount?> UpdateAsync(int id, ChartOfAccount updatedCoa)
        {
            try
            {
                var existingCoa = await _context.ChartOfAccount.FirstOrDefaultAsync(s => s.Id == id);
                if (existingCoa == null) return null;

                existingCoa.HeadName = updatedCoa.HeadName;
                existingCoa.HeadCode = updatedCoa.HeadCode;
                existingCoa.HeadLevel = updatedCoa.HeadLevel;
                existingCoa.HeadType = updatedCoa.HeadType;
                existingCoa.HeadCodeName = updatedCoa.HeadCodeName;
                existingCoa.IsGl = updatedCoa.IsGl;
                existingCoa.IsTransaction = updatedCoa.IsTransaction;
                existingCoa.ParentHead = updatedCoa.ParentHead;
                existingCoa.PHeadName = updatedCoa.PHeadName;
                existingCoa.Balance = updatedCoa.Balance;
                existingCoa.OpeningBalance = updatedCoa.OpeningBalance;

                existingCoa.UpdatedBy = "System";
                existingCoa.UpdatedDate = DateTime.UtcNow;              

                await _context.SaveChangesAsync();
                return existingCoa;
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
            var coa = await _context.ChartOfAccount.FirstOrDefaultAsync(s => s.Id == id);
            if (coa == null) return false;

            coa.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
