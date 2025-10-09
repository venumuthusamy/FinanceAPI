using FinanceApi.Models;
using FinanceApi.Data;
using Microsoft.EntityFrameworkCore;
using FinanceApi.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using Dapper;

namespace FinanceApi.Repositories
{
    public class PurchaseOrderRepository : DynamicRepository,IPurchaseOrderRepository
    {
        private readonly ApplicationDbContext _context;

        public PurchaseOrderRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
        {

        }

        public async Task<IEnumerable<PurchaseOrderDto>> GetAllAsync()
        {
            //return await _context.PurchaseOrder
            //    .Where(c => c.IsActive)
            //    .OrderBy(c => c.Id)
            //    .Select(s => new PurchaseOrderDto
            //    {
            //        Id = s.Id,
            //        PurchaseOrderNo = s.PurchaseOrderNo,
            //        SupplierId = s.SupplierId,
            //        ApproveLevelId = s.ApproveLevelId,
            //        ApprovalStatus = s.ApprovalStatus,
            //        PaymentTermId = s.PaymentTermId,
            //        PaymentTermName = s.PaymentTerms.PaymentTermsName,
            //        CurrencyId = s.CurrencyId,
            //        CurrencyName = s.Currency.CurrencyName,
            //        DeliveryId = s.DeliveryId,
            //        DeliveryName = s.Location.Name,
            //        ContactNumber = s.ContactNumber,
            //        IncotermsId = s.IncotermsId,
            //        PoDate = s.PoDate,
            //        DeliveryDate = s.DeliveryDate,
            //        Remarks = s.Remarks,
            //        FxRate = s.FxRate,
            //        Tax = s.Tax,
            //        Shipping = s.Shipping,
            //        Discount = s.Discount,
            //        SubTotal = s.SubTotal,
            //        NetTotal = s.NetTotal,
            //        PoLines = s.PoLines,
            //        CreatedBy = s.CreatedBy,
            //        CreatedDate = s.CreatedDate,
            //        UpdatedBy = s.UpdatedBy,
            //        UpdatedDate = s.UpdatedDate,
            //        IsActive = s.IsActive
            //    })
            //    .ToListAsync();

            var query = @"
                        SELECT                            
                           po.Id ,
                           po.PurchaseOrderNo,
                           po.SupplierId,
                           ISNULL(s.Name, '') AS SupplierName,
                           po.ApproveLevelId,
                           po.ApprovalStatus,
                           po.PaymentTermId,
                           ISNULL(p.PaymentTermsName, '') AS PaymentTermName,
                           po.CurrencyId,
                           ISNULL(c.CurrencyName, '') AS CurrencyName,
                           po.IncotermsId,
                           po.PoDate,
                           po.DeliveryDate,
                           po.Remarks,
                           po.FxRate,
                           po.Tax,
                           po.Shipping,
                           po.Discount,
                           po.SubTotal,
                           po.NetTotal,
                           po.PoLines,  
                           po.CreatedBy,
                           po.CreatedDate,
                           po.UpdatedBy,
                           po.UpdatedDate,
                           po.IsActive                        

                           FROM PurchaseOrder po
                           LEFT JOIN Suppliers s ON po.SupplierId = s.Id
                           LEFT JOIN PaymentTerms p ON po.PaymentTermId = p.Id
                           LEFT JOIN Currency c ON po.CurrencyId = c.Id
                           WHERE po.IsActive = 1
                           ORDER BY po.Id";

            var rows = await Connection.QueryAsync<PurchaseOrderDto>(query);
            return rows.ToList();
        }




        public async Task<IEnumerable<PurchaseOrderDto>> GetAllDetailswithGRN()
        {
           
            var query = @"
                        SELECT
    po.Id,
    po.PurchaseOrderNo,
    po.SupplierId,
    ISNULL(s.Name, '') AS SupplierName,
    po.ApproveLevelId,
    po.ApprovalStatus,
    po.PaymentTermId,
    ISNULL(p.PaymentTermsName, '') AS PaymentTermName,
    po.CurrencyId,
    ISNULL(c.CurrencyName, '') AS CurrencyName,
    po.IncotermsId,
    po.PoDate,
    po.DeliveryDate,
    po.Remarks,
    po.FxRate,
    po.Tax,
    po.Shipping,
    po.Discount,
    po.SubTotal,
    po.NetTotal,
    po.PoLines,
    po.CreatedBy,
    po.CreatedDate,
    po.UpdatedBy,
    po.UpdatedDate,
    po.IsActive
FROM PurchaseOrder po
LEFT JOIN Suppliers s ON po.SupplierId = s.Id
LEFT JOIN PaymentTerms p ON po.PaymentTermId = p.Id
LEFT JOIN Currency c ON po.CurrencyId = c.Id
WHERE po.IsActive = 1 
  AND po.ApprovalStatus = 2
  AND po.Id NOT IN (
      SELECT POID FROM PurchaseGoodReceipt WHERE POID IS NOT NULL
  )
ORDER BY po.Id;
";

            var rows = await Connection.QueryAsync<PurchaseOrderDto>(query);
            return rows.ToList();
        }


        public async Task<PurchaseOrderDto?> GetByIdAsync(int id)
        {
            const string sql = @"
                           SELECT
                           po.Id ,
                           po.PurchaseOrderNo,
                           po.SupplierId,
                           ISNULL(s.Name, '') AS SupplierName,
                           po.ApproveLevelId,
                           po.ApprovalStatus,
                           po.PaymentTermId,
                           ISNULL(p.PaymentTermsName, '') AS PaymentTermName,
                           po.CurrencyId,
                           ISNULL(c.CurrencyName, '') AS CurrencyName,
                           po.IncotermsId,
                           po.PoDate,
                           po.DeliveryDate,
                           po.Remarks,
                           po.FxRate,
                           po.Tax,
                           po.Shipping,
                           po.Discount,
                           po.SubTotal,
                           po.NetTotal,
                           po.PoLines,  
                           po.CreatedBy,
                           po.CreatedDate,
                           po.UpdatedBy,
                           po.UpdatedDate,
                           po.IsActive                        

                           FROM PurchaseOrder po
                           LEFT JOIN Suppliers s ON po.SupplierId = s.Id
                           LEFT JOIN PaymentTerms p ON po.PaymentTermId = p.Id
                           LEFT JOIN Currency c ON po.CurrencyId = c.Id
                           WHERE po.Id = @id AND po.IsActive = 1;";

            var result = await Connection.QueryFirstOrDefaultAsync<PurchaseOrderDto>(sql, new { id });
            return result;
        }


        public async Task<int> CreateAsync(PurchaseOrder purchaseOrder)
        {
            if (purchaseOrder is null) throw new ArgumentNullException(nameof(purchaseOrder));

            // Ensure timestamps
            if (purchaseOrder.CreatedDate == default) purchaseOrder.CreatedDate = DateTime.UtcNow;
            if (purchaseOrder.UpdatedDate == default) purchaseOrder.UpdatedDate = DateTime.UtcNow;

            // Generate the next PO number from last Id (simple approach)
            const string getLastIdQuery = @"SELECT ISNULL(MAX(Id), 0) FROM PurchaseOrder WITH (HOLDLOCK, TABLOCKX)";
            var lastId = await Connection.ExecuteScalarAsync<int>(getLastIdQuery);
            var nextNumber = lastId + 1;
            purchaseOrder.PurchaseOrderNo = $"PO-{nextNumber:00000}"; // e.g., PO-00001

            const string sql = @"
INSERT INTO PurchaseOrder
(
    PurchaseOrderNo, SupplierId, ApproveLevelId, ApprovalStatus, PaymentTermId,
    CurrencyId, IncotermsId, PoDate, DeliveryDate, Remarks, FxRate, Tax, Shipping,
    Discount, SubTotal, NetTotal, PoLines, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
)
OUTPUT INSERTED.Id
VALUES
(
    @PurchaseOrderNo, @SupplierId, @ApproveLevelId, @ApprovalStatus, @PaymentTermId,
    @CurrencyId, @IncotermsId, @PoDate, @DeliveryDate, @Remarks, @FxRate, @Tax, @Shipping,
    @Discount, @SubTotal, @NetTotal, @PoLines, @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, @IsActive
);


;WITH PRs AS (
    SELECT DISTINCT prNo = LTRIM(RTRIM(prNo))
    FROM OPENJSON(@PoLines)
    WITH (prNo nvarchar(100) '$.prNo')
    WHERE ISNULL(prNo, '') <> ''
)
UPDATE PR
SET PR.Status = @ApprovalStatus,     
    PR.UpdatedDate = SYSUTCDATETIME()
FROM PurchaseRequest PR
JOIN PRs ON PRs.prNo = PR.PurchaseRequestNo;
";


            // Returns the new PO Id
            var newId = await Connection.ExecuteScalarAsync<int>(sql, purchaseOrder);
            return newId;
        }


        public async Task UpdateAsync(PurchaseOrder updatedPurchaseOrder)
        {
            const string query = @"
UPDATE PurchaseOrder 
SET 
    PurchaseOrderNo = @PurchaseOrderNo,
    SupplierId = @SupplierId,
    ApproveLevelId = @ApproveLevelId,
    ApprovalStatus = @ApprovalStatus,
    PaymentTermId = @PaymentTermId,
    CurrencyId = @CurrencyId,
    IncotermsId = @IncotermsId,
    PoDate = @PoDate,
    DeliveryDate = @DeliveryDate,
    Remarks = @Remarks,
    FxRate = @FxRate,
    Tax = @Tax,
    Shipping = @Shipping,
    Discount = @Discount,
    SubTotal = @SubTotal,
    NetTotal = @NetTotal,
    PoLines = @PoLines,
    UpdatedBy = @UpdatedBy,
    UpdatedDate = @UpdatedDate,
    IsActive = @IsActive
WHERE Id = @Id;


;WITH PRs AS (
    SELECT DISTINCT prNo = LTRIM(RTRIM(prNo))
    FROM OPENJSON(@PoLines)
    WITH (prNo nvarchar(100) '$.prNo')
    WHERE ISNULL(prNo, '') <> ''
)
UPDATE PR
SET PR.Status = @ApprovalStatus,       
    PR.UpdatedDate = SYSUTCDATETIME()
FROM PurchaseRequest PR
JOIN PRs ON PRs.prNo = PR.PurchaseRequestNo;
";


            await Connection.ExecuteAsync(query, updatedPurchaseOrder);
        }



        public async Task DeactivateAsync(int id)
        {
            const string query = "UPDATE PurchaseOrder SET IsActive = 0 WHERE ID = @id";
            await Connection.ExecuteAsync(query, new { ID = id });
        }
    }
}
