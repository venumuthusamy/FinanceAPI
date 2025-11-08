// Repositories/QuotationRepository.cs
using System.Data;
using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;

namespace FinanceApi.Repositories
{
    public class QuotationRepository : DynamicRepository, IQuotationRepository
    {
        public QuotationRepository(IDbConnectionFactory cf) : base(cf) { }

        public async Task<IEnumerable<QuotationListDTO>> GetAllAsync()
        {
            const string headerSql = @"
SELECT q.Id,q.Number,q.Status,q.CustomerId,c.CustomerName AS CustomerName,
       q.CurrencyId,q.FxRate,q.PaymentTermsId,q.ValidityDate,
       q.Subtotal,q.TaxAmount,q.Rounding,q.GrandTotal,q.NeedsHodApproval,pt.PaymentTermsName
FROM dbo.Quotation q
LEFT JOIN dbo.Customer c ON c.Id=q.CustomerId
left join dbo.PaymentTerms pt on pt.Id = q.PaymentTermsId
WHERE q.IsActive=1
ORDER BY q.Id DESC;";
            return await Connection.QueryAsync<QuotationListDTO>(headerSql);
        }

        public async Task<QuotationListDTO?> GetByIdAsync(int id)
        {
            const string sql = @"
SELECT q.Id,q.Number,q.Status,q.CustomerId,c.CustomerName AS CustomerName,
       q.CurrencyId,q.FxRate,q.PaymentTermsId,q.ValidityDate,
       q.Subtotal,q.TaxAmount,q.Rounding,q.GrandTotal,q.NeedsHodApproval,cu.CurrencyName,pt.PaymentTermsName,q.ValidityDate
FROM dbo.Quotation q
LEFT JOIN dbo.Customer c ON c.Id=q.CustomerId
LEFT JOIN dbo.Currency cu ON cu.Id = q.CurrencyId  
left join dbo.PaymentTerms pt on pt.Id = q.PaymentTermsId
WHERE q.Id=2 AND q.IsActive=1;;

SELECT l.Id,
       l.QuotationId,
       l.ItemId,
       i.ItemName AS ItemName,  
       l.UomId,                          -- ✅ use UomId
       u.Name AS UomName,                -- ✅ display name
       l.Qty,
       l.UnitPrice,
       l.DiscountPct,
      
      l.TaxMode,
       l.LineNet,
       l.LineTax,
       l.LineTotal
FROM dbo.QuotationLine l
LEFT JOIN dbo.Item i ON i.Id = l.ItemId
LEFT JOIN dbo.Uom   u ON u.Id = l.UomId     -- ✅ join UOM

WHERE l.QuotationId = @Id
ORDER BY l.Id;";
            using var multi = await Connection.QueryMultipleAsync(sql, new { Id = id });
            var head = await multi.ReadFirstOrDefaultAsync<QuotationListDTO>();
            if (head is null) return null;
            head.Lines = (await multi.ReadAsync<QuotationLineDTO>()).ToList();
            return head;
        }

        public async Task<int> CreateAsync(QuotationDTO dto, int userId)
        {
           

            // Lock the Quotation rows while we compute next number
            const string getLastQtNumberSql = @"
SELECT TOP (1) [Number]
FROM dbo.Quotation WITH (UPDLOCK, HOLDLOCK)   -- serialize generators
WHERE [Number] LIKE 'QT-%'
ORDER BY Id DESC;";

            var lastQt = await Connection.QueryFirstOrDefaultAsync<string>(
                getLastQtNumberSql);

            // ---------- 2) Compute next number ----------
            int next = 1;
            if (!string.IsNullOrWhiteSpace(lastQt) && lastQt.StartsWith("QT-"))
            {
                var numericPart = lastQt.Substring(3); // remove "QT-"
                if (int.TryParse(numericPart, out var lastNum))
                    next = lastNum + 1;
            }

            dto.Number = $"QT-{next:D4}";          // e.g. QT-0001
           

            // ---------- 3) Insert header ----------
            const string insertHead = @"
INSERT INTO dbo.Quotation
(Number, Status, CustomerId, CurrencyId, FxRate, PaymentTermsId, ValidityDate,
 Subtotal, TaxAmount, Rounding, GrandTotal, NeedsHodApproval,
 CreatedBy, CreatedDate, IsActive)
OUTPUT INSERTED.Id
VALUES (@Number, @Status, @CustomerId, @CurrencyId, @FxRate, @PaymentTermsId, @ValidityDate,
        @Subtotal, @TaxAmount, @Rounding, @GrandTotal, @NeedsHodApproval,
        @UserId, GETDATE(), 1);";

            var quotationId = await Connection.QueryFirstAsync<int>(insertHead, new
            {
                dto.Number,
                Status = (byte)dto.Status,
                dto.CustomerId,
                dto.CurrencyId,
                dto.FxRate,
                dto.PaymentTermsId,
                dto.ValidityDate,
                dto.Subtotal,
                dto.TaxAmount,
                dto.Rounding,
                dto.GrandTotal,
                dto.NeedsHodApproval,
                UserId = userId
            });

            // ---------- 4) Insert lines (UomId included) ----------
            const string insertLine = @"
INSERT INTO dbo.QuotationLine
(QuotationId, ItemId, UomId, Qty, UnitPrice, DiscountPct, TaxMode, LineNet, LineTax, LineTotal, CreatedBy)
VALUES
(@QuotationId, @ItemId, @UomId, @Qty, @UnitPrice, @DiscountPct, @TaxMode, @LineNet, @LineTax, @LineTotal, @UserId);";

            foreach (var l in dto.Lines)
            {
                await Connection.ExecuteAsync(insertLine, new
                {
                    QuotationId = quotationId,
                    l.ItemId,
                    l.UomId,              // ✅ ID only
                    l.Qty,
                    l.UnitPrice,
                    l.DiscountPct,
                    l.TaxMode,
                    l.LineNet,
                    l.LineTax,
                    l.LineTotal,
                    UserId = userId
                });
            }

            // ---------- 5) Commit ----------
            
            return quotationId;
        }


        public async Task UpdateAsync(QuotationDTO dto, int userId)
        {
           

            const string upd = @"
UPDATE dbo.Quotation
SET Number=@Number, Status=@Status, CustomerId=@CustomerId,
    CurrencyId=@CurrencyId, FxRate=@FxRate, PaymentTermsId=@PaymentTermsId, ValidityDate=@ValidityDate,
    Subtotal=@Subtotal, TaxAmount=@TaxAmount, Rounding=@Rounding, GrandTotal=@GrandTotal,
    NeedsHodApproval=@NeedsHodApproval, UpdatedBy=@UserId, UpdatedDate=GETDATE()
WHERE Id=@Id;";
            await Connection.ExecuteAsync(upd, new
            {
                dto.Number,
                Status = (byte)dto.Status,
                dto.CustomerId,
                dto.CurrencyId,
                dto.FxRate,
                dto.PaymentTermsId,
                dto.ValidityDate,
                dto.Subtotal,
                dto.TaxAmount,
                dto.Rounding,
                dto.GrandTotal,
                dto.NeedsHodApproval,
                UserId = userId,
                dto.Id
            });

            await Connection.ExecuteAsync(
                "DELETE FROM dbo.QuotationLine WHERE QuotationId=@Id",
                new { dto.Id });

            // ✅ Use UomId column (was Uom)
            const string insertLine = @"
INSERT INTO dbo.QuotationLine
(QuotationId, ItemId, UomId, Qty, UnitPrice, DiscountPct, TaxMode, LineNet, LineTax, LineTotal, CreatedBy)
VALUES
(@QuotationId, @ItemId, @UomId, @Qty, @UnitPrice, @DiscountPct, @TaxMode, @LineNet, @LineTax, @LineTotal, @UserId);";

            foreach (var l in dto.Lines)
            {
                await Connection.ExecuteAsync(insertLine, new
                {
                    QuotationId = dto.Id,
                    l.ItemId,
                    l.UomId,                // ✅ pass UomId
                    l.Qty,
                    l.UnitPrice,
                    l.DiscountPct,
                    l.TaxMode,
                    l.LineNet,
                    l.LineTax,
                    l.LineTotal,
                    UserId = userId
                });
            }

           
        }

        public async Task DeactivateAsync(int id, int userId)
        {
            const string sql = "UPDATE dbo.Quotation SET IsActive=0, UpdatedBy=@UserId, UpdatedDate=GETDATE() WHERE Id=@Id;";
            await Connection.ExecuteAsync(sql, new { Id = id, UserId = userId });
        }
    }
}
