using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Repositories
{
    public class PurchaseRequestRepository :IPurchaseRequestRepository
    {
        private readonly ApplicationDbContext _context;

        public PurchaseRequestRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IEnumerable<PurchaseRequestDTO>> GetAllAsync()
        {
            var data = await (from pr in _context.PurchaseRequests
                              join d in _context.Department
                                  on pr.DepartmentID equals d.Id
                              select new PurchaseRequestDTO
                              {
                                  ID = pr.ID,
                                  Requester = pr.Requester,
                                  DepartmentID= pr.DepartmentID,
                                  DepartmentName = d.DepartmentName,   // ✅ DepartmentName here
                                  DeliveryDate = pr.DeliveryDate,
                                  Description = pr.Description,
                                  PurchaseRequestNo = pr.PurchaseRequestNo,
                                  PRLines = pr.PRLines
                              }).ToListAsync();

            return data;
        }




        public async Task<PurchaseRequest?> GetByIdAsync(int id)
        {
            return await _context.PurchaseRequests.FindAsync(id);
        }

        public async Task<PurchaseRequest> AddAsync(PurchaseRequest pr)
        {
            pr.CreatedDate = DateTime.UtcNow;

            // Generate PurchaseRequestNo based on max ID + 1
            var lastPR = await _context.PurchaseRequests
                                .OrderByDescending(x => x.ID)
                                .FirstOrDefaultAsync();

            int nextNumber = (lastPR != null) ? lastPR.ID + 1 : 1;
            pr.PurchaseRequestNo = $"PR-{nextNumber:00000}"; // e.g., PR-00001

            _context.PurchaseRequests.Add(pr);
            await _context.SaveChangesAsync();
            return pr;
        }


        public async Task<bool> UpdateAsync(PurchaseRequest pr)
        {
            var existing = await _context.PurchaseRequests.FindAsync(pr.ID);
            if (existing == null) return false;

            existing.Requester = pr.Requester;
            existing.DepartmentID = pr.DepartmentID;
            existing.DeliveryDate = pr.DeliveryDate;
            existing.Description = pr.Description;
            existing.MultiLoc = pr.MultiLoc;
            existing.Oversea = pr.Oversea;
            existing.PRLines = pr.PRLines;
            existing.UpdatedDate = DateTime.UtcNow;
            existing.UpdateddBy = pr.UpdateddBy;

            _context.PurchaseRequests.Update(existing);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var existing = await _context.PurchaseRequests.FindAsync(id);
            if (existing == null) return false;

            _context.PurchaseRequests.Remove(existing);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
