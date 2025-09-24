using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Repositories
{
    public class ApprovalLevelRepository : IApprovalLevelRepository
    {
        private readonly ApplicationDbContext _context;

        public ApprovalLevelRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ApprovalLevelDTO>> GetAllAsync()
        {
            return await _context.ApprovalLevels
                .Where(a => a.IsActive == true)  // only active
                .Select(a => new ApprovalLevelDTO
                {
                    Id = a.Id,
                    Name = a.Name,
                    Description = a.Description,
                    CreatedBy = a.CreatedBy,
                    CreatedDate = a.CreatedDate,
                    UpdatedBy = a.UpdatedBy,
                    UpdatedDate = a.UpdatedDate,
                    IsActive = a.IsActive
                })
                .ToListAsync();
        }


        // GET BY ID
        public async Task<ApprovalLevelDTO?> GetByIdAsync(int id)
        {
            var a = await _context.ApprovalLevels
                .Where(x => x.Id == id && x.IsActive == true) // only active
                .FirstOrDefaultAsync();

            if (a == null) return null;

            return new ApprovalLevelDTO
            {
                Id = a.Id,
                Name = a.Name,
                Description = a.Description,
                CreatedBy = a.CreatedBy,
                CreatedDate = a.CreatedDate,
                UpdatedBy = a.UpdatedBy,
                UpdatedDate = a.UpdatedDate,
                IsActive = a.IsActive
            };
        }


        // CREATE
        public async Task<ApprovalLevel> AddAsync(ApprovalLevel approvalLevel)
        {
            approvalLevel.CreatedDate = DateTime.UtcNow;
            _context.ApprovalLevels.Add(approvalLevel);
            await _context.SaveChangesAsync();
            return approvalLevel;
        }

        // UPDATE
        public async Task<bool> UpdateAsync(ApprovalLevel approvalLevel)
        {
            var existing = await _context.ApprovalLevels
                .Where(a => a.Id == approvalLevel.Id && a.IsActive == true) // only active
                .FirstOrDefaultAsync();

            if (existing == null) return false;

            existing.Name = approvalLevel.Name;
            existing.Description = approvalLevel.Description;
            existing.UpdatedBy = approvalLevel.UpdatedBy;
            existing.UpdatedDate = DateTime.UtcNow;
            existing.IsActive = approvalLevel.IsActive;

            _context.ApprovalLevels.Update(existing);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var existing = await _context.ApprovalLevels.FindAsync(id);
            if (existing == null) return false;

            // Soft delete: mark as inactive
            existing.IsActive = false;
            existing.UpdatedDate = DateTime.UtcNow;

            _context.ApprovalLevels.Update(existing);
            await _context.SaveChangesAsync();
            return true;
        }

    }
}
