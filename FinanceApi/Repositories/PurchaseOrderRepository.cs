using FinanceApi.Models;
using FinanceApi.Data;
using Microsoft.EntityFrameworkCore;
using FinanceApi.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceApi.Repositories
{
    public class PurchaseOrderRepository : IPurchaseOrderRepository
    {
        private readonly ApplicationDbContext _context;

        public PurchaseOrderRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<PurchaseOrderDto>> GetAllAsync()
        {
            return await _context.PurchaseOrder
                .Where(c => c.IsActive)
                .OrderBy(c => c.Id)
                .Select(s => new PurchaseOrderDto
                {
                    Id = s.Id,
                    PurchaseOrderNo = s.PurchaseOrderNo,
                    SupplierId = s.SupplierId,
                    ApproveLevelId = s.ApproveLevelId,
                    PaymentTermId = s.PaymentTermId,
                    CurrencyId = s.CurrencyId,
                    DeliveryId  = s.DeliveryId,
                    ContactNumber = s.ContactNumber,
                    IncotermsId = s.IncotermsId,
                    PoDate = s.PoDate,
                    DeliveryDate = s.DeliveryDate,
                    Remarks = s.Remarks,
                    FxRate = s.FxRate,
                    Tax = s.Tax,
                    Shipping = s.Shipping,
                    Discount = s.Discount,
                    SubTotal =s.SubTotal,
                    NetTotal =s.NetTotal,
                    PoLines = s.PoLines,  
                    CreatedBy = s.CreatedBy,
                    CreatedDate = s.CreatedDate,
                    UpdatedBy = s.UpdatedBy,
                    UpdatedDate = s.UpdatedDate,
                    IsActive = s.IsActive
                })
                .ToListAsync();
        }


        public async Task<PurchaseOrderDto?> GetByIdAsync(int id)
        {
            return await _context.PurchaseOrder
                .Where(s => s.Id == id)
                .Where(c => c.IsActive)
                .Select(s => new PurchaseOrderDto
                {
                    Id = s.Id,
                    PurchaseOrderNo = s.PurchaseOrderNo,
                    SupplierId = s.SupplierId,
                    ApproveLevelId = s.ApproveLevelId,
                    PaymentTermId = s.PaymentTermId,
                    CurrencyId = s.CurrencyId,
                    DeliveryId = s.DeliveryId,
                    ContactNumber = s.ContactNumber,
                    IncotermsId = s.IncotermsId,
                    PoDate = s.PoDate,
                    DeliveryDate = s.DeliveryDate,
                    Remarks = s.Remarks,
                    FxRate = s.FxRate,
                    Tax = s.Tax,
                    Shipping = s.Shipping,
                    Discount = s.Discount,
                    SubTotal = s.SubTotal,
                    NetTotal = s.NetTotal,
                    PoLines = s.PoLines,
                    CreatedBy = s.CreatedBy,
                    CreatedDate = s.CreatedDate,
                    UpdatedBy = s.UpdatedBy,
                    UpdatedDate = s.UpdatedDate,
                    IsActive = s.IsActive
                })
                .FirstOrDefaultAsync();
        }


        public async Task<PurchaseOrder> CreateAsync(PurchaseOrder purchaseOrder)
        {
            try
            {
                purchaseOrder.CreatedBy = "System";
                purchaseOrder.CreatedDate = DateTime.UtcNow;
                purchaseOrder.IsActive = true;
                // Generate PurchaseOrderNo based on max ID + 1
                var lastPO = await _context.PurchaseOrder
                                    .OrderByDescending(x => x.Id)
                                    .FirstOrDefaultAsync();

                int nextNumber = (lastPO != null) ? lastPO.Id + 1 : 1;
                purchaseOrder.PurchaseOrderNo = $"PO-{nextNumber:00000}"; // e.g., PO-00001
                _context.PurchaseOrder.Add(purchaseOrder);
                await _context.SaveChangesAsync();
                return purchaseOrder;

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

        public async Task<PurchaseOrder?> UpdateAsync(int id, PurchaseOrder updatedPurchaseOrder)
        {
            try
            {
                var existingPurchaseOrder = await _context.PurchaseOrder.FirstOrDefaultAsync(s => s.Id == id);
                if (existingPurchaseOrder == null) return null;

                // Manually update only scalar properties (excluding Id)
                existingPurchaseOrder.PurchaseOrderNo = updatedPurchaseOrder.PurchaseOrderNo;
                existingPurchaseOrder.SupplierId = updatedPurchaseOrder.SupplierId;
                existingPurchaseOrder.ApproveLevelId = updatedPurchaseOrder.ApproveLevelId;
                existingPurchaseOrder.PaymentTermId = updatedPurchaseOrder.PaymentTermId;
                existingPurchaseOrder.CurrencyId = updatedPurchaseOrder.CurrencyId;
                existingPurchaseOrder.DeliveryId = updatedPurchaseOrder.DeliveryId;
                existingPurchaseOrder.ContactNumber = updatedPurchaseOrder.ContactNumber;
                existingPurchaseOrder.IncotermsId = updatedPurchaseOrder.IncotermsId;
                existingPurchaseOrder.PoDate = updatedPurchaseOrder.PoDate;
                existingPurchaseOrder.DeliveryDate = updatedPurchaseOrder.DeliveryDate;
                existingPurchaseOrder.Remarks = updatedPurchaseOrder.Remarks;
                existingPurchaseOrder.FxRate = updatedPurchaseOrder.FxRate;
                existingPurchaseOrder.Tax = updatedPurchaseOrder.Tax;
                existingPurchaseOrder.Shipping = updatedPurchaseOrder.Shipping;
                existingPurchaseOrder.Discount = updatedPurchaseOrder.Discount;
                existingPurchaseOrder.SubTotal = updatedPurchaseOrder.SubTotal;
                existingPurchaseOrder.NetTotal = updatedPurchaseOrder.NetTotal;
                existingPurchaseOrder.PoLines = updatedPurchaseOrder.PoLines;
                existingPurchaseOrder.UpdatedBy = updatedPurchaseOrder.UpdatedBy;
                existingPurchaseOrder.UpdatedDate = updatedPurchaseOrder.UpdatedDate;
                existingPurchaseOrder.IsActive = updatedPurchaseOrder.IsActive;
                existingPurchaseOrder.PoLines = updatedPurchaseOrder.PoLines;               

                await _context.SaveChangesAsync();
                return existingPurchaseOrder;
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
            var purchaseOrder = await _context.PurchaseOrder.FirstOrDefaultAsync(s => s.Id == id);
            if (purchaseOrder == null) return false;

            purchaseOrder.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
