// Data/AccountsPayableRepository.cs
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

        /// <summary>
        /// AP invoice list – includes supplier, totals, paid + outstanding.
        /// </summary>
        public async Task<IEnumerable<ApInvoiceDTO>> GetApInvoicesAsync()
        {
            const string sql = @"
SELECT
    si.Id,
    po.SupplierId,
    s.Name AS SupplierName,
    si.InvoiceNo,
    si.InvoiceDate,
    ISNULL(po.DeliveryDate, si.InvoiceDate) AS DueDate,
    GrandTotal =
        ISNULL(si.Amount, 0) + ISNULL(si.Tax, 0),
    PaidAmount =
        ISNULL(pay.PaidAmount, 0),
    OutstandingAmount =
        (ISNULL(si.Amount, 0) + ISNULL(si.Tax, 0))
        - ISNULL(pay.PaidAmount, 0),
    si.Status
FROM dbo.SupplierInvoicePin si
LEFT JOIN dbo.PurchaseGoodReceipt gr ON gr.Id = si.GrnId
LEFT JOIN dbo.PurchaseOrder      po ON po.Id = gr.POID
LEFT JOIN dbo.Suppliers          s  ON s.Id = po.SupplierId
OUTER APPLY
(
    SELECT SUM(sp.Amount) AS PaidAmount
    FROM dbo.SupplierPayment sp
    WHERE sp.SupplierInvoiceId = si.Id
      AND sp.IsActive = 1
      AND sp.Status   = 1
) pay
WHERE si.IsActive = 1 and Status =3
ORDER BY si.InvoiceDate DESC, si.Id DESC;";

            using var conn = Connection;
            return await conn.QueryAsync<ApInvoiceDTO>(sql);
        }


        public async Task<IEnumerable<ApInvoiceDTO>> GetApInvoicesBySupplierAsync(int supplierId)
        {
            const string sql = @"
SELECT
    si.Id,
    po.SupplierId,
    s.Name AS SupplierName,
    si.InvoiceNo,
    si.InvoiceDate,
    ISNULL(po.DeliveryDate, si.InvoiceDate) AS DueDate,
    GrandTotal =
        ISNULL(si.Amount, 0) + ISNULL(si.Tax, 0),
    PaidAmount =
        ISNULL(pay.PaidAmount, 0),
    OutstandingAmount =
        (ISNULL(si.Amount, 0) + ISNULL(si.Tax, 0))
        - ISNULL(pay.PaidAmount, 0),
    si.Status
FROM dbo.SupplierInvoicePin si
LEFT JOIN dbo.PurchaseGoodReceipt gr ON gr.Id = si.GrnId
LEFT JOIN dbo.PurchaseOrder      po ON po.Id = gr.POID
LEFT JOIN dbo.Suppliers          s  ON s.Id = po.SupplierId
OUTER APPLY
(
    SELECT SUM(sp.Amount) AS PaidAmount
    FROM dbo.SupplierPayment sp
    WHERE sp.SupplierInvoiceId = si.Id
      AND sp.IsActive = 1
      AND sp.Status   = 1
) pay
WHERE si.IsActive = 1 and Status=3
  AND po.SupplierId = @SupplierId
ORDER BY si.InvoiceDate DESC, si.Id DESC;";

            using var conn = Connection;
            return await conn.QueryAsync<ApInvoiceDTO>(sql, new { SupplierId = supplierId });
        }

        /// <summary>
        /// 3-way match: PO vs GRN vs PIN.
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

    -- Invoice amount = Amount + Tax
    ISNULL(si.Amount, 0)  AS InvoiceAmount,

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
