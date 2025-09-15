using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Repositories
{
    public class OpeningBalanceRepository : IOpeningBalanceRepository
    {
        private readonly ApplicationDbContext _context;

        public OpeningBalanceRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<OpeningBalanceDto>> GetAllAsync()
        {
            try
            {
                var result = await _context.OpeningBalance
               .Include(c => c.ChartOfAccount)
               .Where(c => c.IsActive)
               .OrderBy(c => c.Id)
               .Select(c => new OpeningBalanceDto
               {
                   Id = c.Id,
                   Date = c.Date,
                   AccountHeadId = c.AccountHeadId,
                   AccountHeadName = c.ChartOfAccount != null ? c.ChartOfAccount.HeadName : string.Empty,
                   BalanceType = c.BalanceType,
                   Amount = c.Amount,
                   Remark = c.Remark,
                   CreatedBy = c.CreatedBy,
                   CreatedDate = c.CreatedDate,
                   UpdatedBy = c.UpdatedBy,
                   UpdatedDate = c.UpdatedDate,
                   IsActive = c.IsActive

               })
               .ToListAsync();
                return result;

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

        public async Task<OpeningBalanceDto?> GetByIdAsync(int id)
        {
            var result = await _context.OpeningBalance
                .Include(c =>c.ChartOfAccount)
                .Where(c => c.Id == id)
                .Where(c => c.IsActive)
                .Select(c => new OpeningBalanceDto
                {
                    Id = c.Id,
                    Date = c.Date,
                    AccountHeadId = c.AccountHeadId,
                    AccountHeadName = c.ChartOfAccount != null ? c.ChartOfAccount.HeadName : string.Empty,
                    BalanceType = c.BalanceType,
                    Amount = c.Amount,
                    Remark = c.Remark,
                    CreatedBy = c.CreatedBy,
                    CreatedDate = c.CreatedDate,
                    UpdatedBy = c.UpdatedBy,
                    UpdatedDate = c.UpdatedDate,
                    IsActive = c.IsActive

                })
                .FirstOrDefaultAsync();
            return result;
        }

        public async Task<OpeningBalance> CreateAsync(OpeningBalance openingBalance)
        {
            try
            {
                
                openingBalance.CreatedDate = DateTime.UtcNow;
                openingBalance.CreatedBy = "System";
                openingBalance.IsActive = true;


                var chartAccount = await _context.ChartOfAccount
                         .FirstOrDefaultAsync(c => c.Id == openingBalance.AccountHeadId);

                if (chartAccount != null)
                {
                    if (openingBalance.BalanceType == AccountType.Debit)
                    {
                        chartAccount.Balance = (chartAccount.Balance == null ? 0 : chartAccount.Balance) + openingBalance.Amount;
                        chartAccount.OpeningBalance = (chartAccount.OpeningBalance == null ? 0 : chartAccount.OpeningBalance) + openingBalance.Amount;
                        _context.ChartOfAccount.Update(chartAccount);
                    }
                    else
                    {
                        chartAccount.Balance = (chartAccount.Balance == null ? 0 : chartAccount.Balance) - openingBalance.Amount;
                        chartAccount.OpeningBalance = (chartAccount.OpeningBalance == null ? 0 : chartAccount.OpeningBalance) - openingBalance.Amount;
                        _context.ChartOfAccount.Update(chartAccount);
                    }

                }


                _context.OpeningBalance.Add(openingBalance);
                await _context.SaveChangesAsync();
                return openingBalance;

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

        public async Task<OpeningBalance?> UpdateAsync(int id, OpeningBalance updatedOpeningBalance)
        {
            try
            {
                var existingOpeningBalance = await _context.OpeningBalance.FirstOrDefaultAsync(s => s.Id == id);
                if (existingOpeningBalance == null) return null;

                // Manually update only scalar properties (excluding Id)
                existingOpeningBalance.Date = updatedOpeningBalance.Date; 
                existingOpeningBalance.AccountHeadId = updatedOpeningBalance.AccountHeadId;
                existingOpeningBalance.BalanceType = updatedOpeningBalance.BalanceType;
                existingOpeningBalance.Amount = updatedOpeningBalance.Amount;
                existingOpeningBalance.Remark = updatedOpeningBalance.Remark;
                existingOpeningBalance.UpdatedBy = "System";
                existingOpeningBalance.UpdatedDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                var chartAccount = await _context.ChartOfAccount
                                   .FirstOrDefaultAsync(c => c.Id == existingOpeningBalance.AccountHeadId);

                if (chartAccount != null)
                {
                    if(existingOpeningBalance.BalanceType == AccountType.Debit)
                    {
                        chartAccount.Balance = (chartAccount.Balance == null ? 0 : chartAccount.Balance) + existingOpeningBalance.Amount;
                        chartAccount.OpeningBalance = (chartAccount.OpeningBalance == null ? 0 : chartAccount.OpeningBalance) + existingOpeningBalance.Amount;
                        _context.ChartOfAccount.Update(chartAccount);
                    }
                    else {
                        chartAccount.Balance = (chartAccount.Balance == null ? 0 : chartAccount.Balance) - existingOpeningBalance.Amount;
                        chartAccount.OpeningBalance = (chartAccount.OpeningBalance == null ? 0 : chartAccount.OpeningBalance) - existingOpeningBalance.Amount;
                        _context.ChartOfAccount.Update(chartAccount);
                    }
                    
                }

                await _context.SaveChangesAsync();
                return existingOpeningBalance;
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
            var openingBalance = await _context.OpeningBalance.FirstOrDefaultAsync(s => s.Id == id);
            if (openingBalance == null) return false;

            openingBalance.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
