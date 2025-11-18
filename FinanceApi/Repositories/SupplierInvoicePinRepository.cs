// Repositories/SupplierInvoiceRepository.cs
using System.Data;
using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Repositories
{
    public class SupplierInvoicePinRepository : DynamicRepository, ISupplierInvoicePinRepository
    {
        public SupplierInvoicePinRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory) { }

        // ---------- READ ----------
        public async Task<IEnumerable<SupplierInvoicePin>> GetAllAsync()
        {
            const string sql = @"
SELECT *
FROM dbo.SupplierInvoicePin
WHERE IsActive = 1
ORDER BY Id DESC;";

            return await Connection.QueryAsync<SupplierInvoicePin>(sql);
        }

        public async Task<SupplierInvoicePinDTO> GetByIdAsync(int id)
        {
            const string sql = @"
    SELECT si.*,grp.GrnNo,po.Tax,s.Id   AS SupplierId,s.Name as SupplierName FROM SupplierInvoicePin as si
inner join PurchaseGoodReceipt as grp on grp.Id = si.grnid
inner join PurchaseOrder as po on po.Id= grp.POID
inner join Suppliers as s on  s.id = si.supplierid
WHERE si.Id = @Id;";
            return await Connection.QuerySingleAsync<SupplierInvoicePinDTO>(sql, new { Id = id });
        }

        // ---------- CREATE ----------
        public async Task<int> CreateAsync(SupplierInvoicePin pin)
        {
            // Generate next PIN-#### similar to PR repo style
            const string getLastNo = @"
SELECT TOP 1 InvoiceNo
FROM dbo.SupplierInvoicePin
WHERE ISNUMERIC(SUBSTRING(InvoiceNo, 5, LEN(InvoiceNo))) = 1
ORDER BY Id DESC;";

            var last = await Connection.QueryFirstOrDefaultAsync<string>(getLastNo);

            int next = 1;
            if (!string.IsNullOrWhiteSpace(last) && last.StartsWith("PIN-"))
            {
                var numeric = last.Substring(4);
                if (int.TryParse(numeric, out var n)) next = n + 1;
            }

            pin.InvoiceNo = $"PIN-{next:D4}";
            pin.CreatedDate = pin.UpdatedDate = DateTime.UtcNow;

            const string insert = @"
INSERT INTO dbo.SupplierInvoicePin
(InvoiceNo, InvoiceDate, Amount, Tax, CurrencyId, Status, LinesJson,
 IsActive, CreatedDate, UpdatedDate, CreatedBy, UpdatedBy, GrnId,SupplierId)
OUTPUT INSERTED.Id
VALUES
(@InvoiceNo, @InvoiceDate, @Amount, @Tax, @CurrencyId, @Status, @LinesJson,
 1, @CreatedDate, @UpdatedDate, @CreatedBy, @UpdatedBy, @GrnId,@SupplierId);";

            return await Connection.QueryFirstAsync<int>(insert, pin);
        }

        // ---------- UPDATE ----------
        public async Task UpdateAsync(SupplierInvoicePin pin)
        {
            pin.UpdatedDate = DateTime.UtcNow;

            const string update = @"
UPDATE dbo.SupplierInvoicePin SET
  InvoiceDate = @InvoiceDate,
  Amount      = @Amount,
  Tax         = @Tax,
  CurrencyId    = @CurrencyId,
  Status      = @Status,
  LinesJson   = @LinesJson,
  GrnId       = @GrnId,
  UpdatedDate = @UpdatedDate,
  UpdatedBy   = @UpdatedBy
WHERE Id = @Id;";

            await Connection.ExecuteAsync(update, pin);
        }

        // ---------- DEACTIVATE ----------
        public async Task DeactivateAsync(int id)
        {
            const string sql = @"UPDATE dbo.SupplierInvoicePin SET IsActive = 0 WHERE Id = @Id;";
            await Connection.ExecuteAsync(sql, new { Id = id });
        }
        public async Task<ThreeWayMatchDTO?> GetThreeWayMatchAsync(int pinId)
        {
            const string sql = @"
-- 1) PO aggregate from PoLines JSON using PIN -> GRN -> PO chain
WITH PoAgg AS (
    SELECT
        po.Id              AS PoId,
        po.PurchaseOrderNo AS PoNo,
        SUM(TRY_CONVERT(decimal(18,4), JSON_VALUE(l.value, '$.qty')))   AS PoQty,
       po.NetTotal AS PoTotal
    FROM dbo.SupplierInvoicePin sip
    INNER JOIN dbo.PurchaseGoodReceipt gr ON gr.Id = sip.GrnId
    INNER JOIN dbo.PurchaseOrder po       ON po.Id = gr.POID
    CROSS APPLY OPENJSON(po.PoLines) AS l
    WHERE sip.Id = @PinId
    GROUP BY po.Id, po.PurchaseOrderNo,po.NetTotal
),
-- 2) GRN aggregate from GRNJson JSON
GrnAgg AS (
    SELECT
        gr.Id              AS GrnId,
        gr.GrnNo           AS GrnNo,
        SUM(TRY_CONVERT(decimal(18,4), JSON_VALUE(j.value, '$.qtyReceived'))) AS GrnReceivedQty,
        ISNULL(gr.OverReceiptTolerance, 0)                                  AS Tolerance
    FROM dbo.SupplierInvoicePin sip
    INNER JOIN dbo.PurchaseGoodReceipt gr ON gr.Id = sip.GrnId
    CROSS APPLY OPENJSON(gr.GRNJson) AS j
    WHERE sip.Id = @PinId
    GROUP BY gr.Id, gr.GrnNo, gr.OverReceiptTolerance
),
-- 3) PIN aggregate from LinesJson JSON
PinAgg AS (
    SELECT
        sip.Id        AS PinId,
        sip.InvoiceNo AS PinNo,
        SUM(TRY_CONVERT(decimal(18,4), JSON_VALUE(j.value, '$.qty')))  AS PinQty,
       sip.Amount AS PinTotal
    FROM dbo.SupplierInvoicePin sip
    CROSS APPLY OPENJSON(sip.LinesJson) AS j
    WHERE sip.Id = @PinId
    GROUP BY sip.Id, sip.InvoiceNo,sip.Amount
)
SELECT TOP 1
    pa.PoId,
    pa.PoNo,
    ISNULL(pa.PoQty, 0)                                        AS PoQty,
    CASE WHEN ISNULL(pa.PoQty, 0) = 0
         THEN 0
         ELSE pa.PoTotal / NULLIF(pa.PoQty, 0)
    END                                                        AS PoPrice,
    ISNULL(pa.PoTotal, 0)                                      AS PoTotal,

    ISNULL(ga.GrnId, 0)                                        AS GrnId,
    ISNULL(ga.GrnNo, '')                                       AS GrnNo,
    ISNULL(ga.GrnReceivedQty, 0)                               AS GrnReceivedQty,
    ISNULL(ga.GrnReceivedQty, 0) - ISNULL(pa.PoQty, 0)         AS GrnVarianceQty,
    CASE 
        WHEN ga.GrnId IS NULL THEN 'No GRN'
        WHEN ABS(ISNULL(ga.GrnReceivedQty, 0) - ISNULL(pa.PoQty, 0)) 
             <= ISNULL(ga.Tolerance, 0) THEN 'OK'
        ELSE 'Check'
    END                                                        AS GrnStatus,

    pin.PinId,
    pin.PinNo,
    ISNULL(pin.PinQty, 0)                                      AS PinQty,
    ISNULL(pin.PinTotal, 0)                                    AS PinTotal,

    CASE 
        WHEN pa.PoQty IS NULL THEN 0
        WHEN ABS(ISNULL(pin.PinQty, 0) - ISNULL(pa.PoQty, 0)) > 0.0001 THEN 0
        WHEN ABS(ISNULL(pin.PinTotal, 0) - ISNULL(pa.PoTotal, 0)) > 0.01 THEN 0
        ELSE 1
    END                                                        AS PinMatch
FROM PinAgg pin
LEFT JOIN PoAgg pa ON 1 = 1
LEFT JOIN GrnAgg ga ON 1 = 1;";

            return await Connection.QueryFirstOrDefaultAsync<ThreeWayMatchDTO>(
                sql,
                new { PinId = pinId }
            );
        }


        public async Task FlagForReviewAsync(int pinId, string userName)
        {
            const string sql = @"
UPDATE dbo.SupplierInvoicePin
SET Status     = 2,               -- 2 = Flag for Review
    UpdatedBy  = @UserName,
    UpdatedDate = SYSDATETIME()
WHERE Id = @PinId;";

            await Connection.ExecuteAsync(sql, new
            {
                PinId = pinId,
                UserName = userName
            });
        }

        public async Task PostToApAsync(int pinId, string userName)
        {
            const string sql = @"
BEGIN

    -- 1) Supplier Invoice PIN → 3 (Posted to A/P)
    UPDATE dbo.SupplierInvoicePin
    SET Status      = 3,               -- 3 = Posted to A/P
        UpdatedBy   = @UserName,
        UpdatedDate = SYSDATETIME()
    WHERE Id = @PinId;

    -- 2) Related Debit Note(s) → 2  (Closed / Posted)
    UPDATE dbo.SupplierDebitNote
    SET Status      = 2,               -- 2 = Posted / Final
        UpdatedBy   = @UserName,
        UpdatedDate = SYSDATETIME()
    WHERE PinId = @PinId;

END;";

            await Connection.ExecuteAsync(sql, new
            {
                PinId = pinId,
                UserName = userName
            });
        }


    }
}
