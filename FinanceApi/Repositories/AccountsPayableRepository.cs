using System.Data;
using Dapper;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace FinanceApi.Data
{
    public class AccountsPayableRepository : IAccountsPayableRepository
    {
        private readonly IConfiguration _config;
        private IDbConnection Connection => new SqlConnection(_config.GetConnectionString("DefaultConnection"));

        public AccountsPayableRepository(IConfiguration config)
        {
            _config = config;
        }
        public async Task<IEnumerable<ApInvoiceDTO>> GetApInvoicesAsync()
        {
            const string sql = @"
SELECT
    si.Id,
    po.SupplierId,
    s.Name AS SupplierName,
    si.InvoiceNo,
    si.InvoiceDate,

    -- Due date from PaymentTermsName -> PaymentTermDays (calculated in CROSS APPLY)
    CASE 
        WHEN pt.Id IS NOT NULL 
            THEN DATEADD(DAY, ISNULL(ptd.PaymentTermDays, 0), si.InvoiceDate)
        ELSE ISNULL(po.DeliveryDate, si.InvoiceDate)
    END AS DueDate,

    -- Gross invoice amount (Amount + Tax)
    GrandTotal =
        ISNULL(si.Amount, 0) + ISNULL(si.Tax, 0),

    -- Total payments
    PaidAmount =
        ISNULL(pay.PaidAmount, 0),

    -- Debit note aggregate
    DebitNoteAmount =
        ISNULL(dn.DebitNoteAmount, 0),

    dn.DebitNoteNo,
    dn.DebitNoteDate,

    -- Net Outstanding = Invoice - Payment - DebitNote
    OutstandingAmount =
        (ISNULL(si.Amount, 0) + ISNULL(si.Tax, 0))
        - ISNULL(pay.PaidAmount, 0)
        - ISNULL(dn.DebitNoteAmount, 0),

    si.Status
FROM dbo.SupplierInvoicePin si
LEFT JOIN dbo.PurchaseGoodReceipt gr ON gr.Id = si.GrnId
LEFT JOIN dbo.PurchaseOrder      po ON po.Id = gr.POID
LEFT JOIN dbo.Suppliers          s  ON s.Id = po.SupplierId
LEFT JOIN dbo.PaymentTerms       pt ON pt.Id = po.PaymentTermId

-- 🔹 Calculate PaymentTermDays from PaymentTermsName
CROSS APPLY
(
    SELECT
        PaymentTermDays =
            COALESCE(
                TRY_CAST(
                    SUBSTRING(
                        pt.PaymentTermsName,
                        PATINDEX('%[0-9]%', pt.PaymentTermsName + '0'),
                        3
                    ) AS INT
                ),
                CASE
                    WHEN pt.PaymentTermsName LIKE '%immediate%' THEN 0
                    WHEN pt.PaymentTermsName LIKE '%cash%'      THEN 0
                    WHEN pt.PaymentTermsName LIKE '%advance%'   THEN 0
                    WHEN pt.PaymentTermsName LIKE '%weekly%'    THEN 7
                    WHEN pt.PaymentTermsName LIKE '%bi-week%' OR pt.PaymentTermsName LIKE '%biweek%' THEN 14
                    WHEN pt.PaymentTermsName LIKE '%30%' THEN 30
                    WHEN pt.PaymentTermsName LIKE '%45%' THEN 45
                    WHEN pt.PaymentTermsName LIKE '%60%' THEN 60
                    WHEN pt.PaymentTermsName LIKE '%90%' THEN 90
                    ELSE 0
                END
            )
) ptd

-- 🔹 Payments
OUTER APPLY
(
    SELECT SUM(sp.Amount) AS PaidAmount
    FROM dbo.SupplierPayment sp
    WHERE sp.SupplierInvoiceId = si.Id
      AND sp.IsActive = 1
      AND sp.Status   = 1       -- 1 = Posted payment
) pay

-- 🔹 Debit notes (aggregate)
OUTER APPLY
(
    SELECT
        SUM(ABS(dn.Amount))      AS DebitNoteAmount,
        MAX(dn.DebitNoteNo)      AS DebitNoteNo,    -- if multiple DNs, last one; change to STRING_AGG if needed
        MAX(dn.CreatedDate)      AS DebitNoteDate   -- or dn.NoteDate
    FROM dbo.SupplierDebitNote dn
    WHERE dn.PinId    = si.Id
      AND dn.IsActive = 1
      AND dn.Status   = 2       -- 2 = Posted DN (adjust if your status is different)
) dn

WHERE si.IsActive = 1
  AND si.Status   = 3           -- 3 = Posted to AP (PIN status)
ORDER BY si.InvoiceDate DESC, si.Id DESC;";

            using var conn = Connection;
            return await conn.QueryAsync<ApInvoiceDTO>(sql);
        }


        // =========================================================
        //  AP INVOICE LIST  (single supplier)
        // =========================================================
        public async Task<IEnumerable<ApInvoiceDTO>> GetApInvoicesBySupplierAsync(int supplierId)
        {
            const string sql = @"
SELECT
    si.Id,
    po.SupplierId,
    s.Name AS SupplierName,
    si.InvoiceNo,
    si.InvoiceDate,

    CASE 
        WHEN pt.Id IS NOT NULL 
            THEN DATEADD(DAY, ISNULL(ptd.PaymentTermDays, 0), si.InvoiceDate)
        ELSE ISNULL(po.DeliveryDate, si.InvoiceDate)
    END AS DueDate,

    GrandTotal =
        ISNULL(si.Amount, 0) + ISNULL(si.Tax, 0),

    PaidAmount =
        ISNULL(pay.PaidAmount, 0),

    DebitNoteAmount =
        ISNULL(dn.DebitNoteAmount, 0),

    OutstandingAmount =
        (ISNULL(si.Amount, 0) + ISNULL(si.Tax, 0))
        - ISNULL(pay.PaidAmount, 0)
        - ISNULL(dn.DebitNoteAmount, 0),

    si.Status
FROM dbo.SupplierInvoicePin si
LEFT JOIN dbo.PurchaseGoodReceipt gr ON gr.Id = si.GrnId
LEFT JOIN dbo.PurchaseOrder      po ON po.Id = gr.POID
LEFT JOIN dbo.Suppliers          s  ON s.Id = po.SupplierId
LEFT JOIN dbo.PaymentTerms       pt ON pt.Id = po.PaymentTermId

CROSS APPLY
(
    SELECT
        PaymentTermDays =
            COALESCE(
                TRY_CAST(
                    SUBSTRING(
                        pt.PaymentTermsName,
                        PATINDEX('%[0-9]%', pt.PaymentTermsName + '0'),
                        3
                    ) AS INT
                ),
                CASE
                    WHEN pt.PaymentTermsName LIKE '%immediate%' THEN 0
                    WHEN pt.PaymentTermsName LIKE '%cash%'      THEN 0
                    WHEN pt.PaymentTermsName LIKE '%advance%'   THEN 0
                    WHEN pt.PaymentTermsName LIKE '%weekly%'    THEN 7
                    WHEN pt.PaymentTermsName LIKE '%bi-week%' OR pt.PaymentTermsName LIKE '%biweek%' THEN 14
                    WHEN pt.PaymentTermsName LIKE '%30%' THEN 30
                    WHEN pt.PaymentTermsName LIKE '%45%' THEN 45
                    WHEN pt.PaymentTermsName LIKE '%60%' THEN 60
                    WHEN pt.PaymentTermsName LIKE '%90%' THEN 90
                    ELSE 0
                END
            )
) ptd

OUTER APPLY
(
    SELECT SUM(sp.Amount) AS PaidAmount
    FROM dbo.SupplierPayment sp
    WHERE sp.SupplierInvoiceId = si.Id
      AND sp.IsActive = 1
      AND sp.Status   = 1
) pay

OUTER APPLY
(
    SELECT SUM(ABS(dn.Amount)) AS DebitNoteAmount
    FROM dbo.SupplierDebitNote dn
    WHERE dn.PinId    = si.Id
      AND dn.IsActive = 1
      AND dn.Status   = 2
) dn

WHERE si.IsActive = 1
  AND si.Status   = 3
  AND po.SupplierId = @SupplierId
ORDER BY si.InvoiceDate DESC, si.Id DESC;";

            using var conn = Connection;
            return await conn.QueryAsync<ApInvoiceDTO>(sql, new { SupplierId = supplierId });
        }

        // GetMatchListAsync you already have – keep as-is
    

/// <summary>
/// 3-way match: PO vs GRN vs PIN.
/// (unchanged – still compare PO NetTotal vs PIN Amount/Tax)
/// </summary>
public async Task<IEnumerable<ApMatchDTO>> GetMatchListAsync()
        {
            const string sql = @"
SELECT TOP (200)
    po.PurchaseOrderNo              AS PoNo,
    gr.GrnNo                        AS GrnNo,
    si.InvoiceNo                    AS InvoiceNo,
    s.Name                          AS SupplierName,

    -- PO amount (NetTotal)
    ISNULL(po.NetTotal, 0)          AS PoAmount,

    -- Invoice amount = Amount (+ Tax if you like)
    ISNULL(si.Amount, 0)            AS InvoiceAmount,

    CASE
        WHEN ABS(
                ISNULL(po.NetTotal, 0)
              - (ISNULL(si.Amount, 0))
             ) < 0.01
            THEN 'Matched'

        WHEN ABS(
                ISNULL(po.NetTotal, 0)
              - (ISNULL(si.Amount, 0) + ISNULL(si.Tax, 0))
             ) < 1
            THEN 'Warning'

        ELSE 'Mismatch'
    END AS [Status]
FROM dbo.SupplierInvoicePin si
LEFT JOIN dbo.PurchaseGoodReceipt gr
       ON gr.Id = si.GrnId
LEFT JOIN dbo.PurchaseOrder po
       ON po.Id = gr.POID
LEFT JOIN dbo.Suppliers s
       ON s.Id = po.SupplierId
WHERE si.IsActive = 1
  AND si.Status   = 3
ORDER BY si.InvoiceDate DESC, si.Id DESC;
;";

            using var conn = Connection;
            return await conn.QueryAsync<ApMatchDTO>(sql);
        }
    }
}
