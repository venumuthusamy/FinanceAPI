// Data/AccountsPayableRepository.cs
using Dapper;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

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

        // ===================== AP INVOICES – ALL =====================
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
        ISNULL(si.Amount, 0),

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
        (ISNULL(si.Amount, 0))
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
        MAX(dn.DebitNoteNo)      AS DebitNoteNo,    
        MAX(dn.CreatedDate)      AS DebitNoteDate   
    FROM dbo.SupplierDebitNote dn
    WHERE dn.PinId    = si.Id
      AND dn.IsActive = 1
      AND dn.Status   = 2       
) dn

WHERE si.IsActive = 1
  AND si.Status   = 3           
ORDER BY si.InvoiceDate DESC, si.Id DESC;";

            using var conn = Connection;
            return await conn.QueryAsync<ApInvoiceDTO>(sql);
        }

        // ===================== AP INVOICES – BY SUPPLIER =====================
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
        ISNULL(si.Amount, 0),

    PaidAmount =
        ISNULL(pay.PaidAmount, 0),

    DebitNoteAmount =
        ISNULL(dn.DebitNoteAmount, 0),

    OutstandingAmount =
        (ISNULL(si.Amount, 0))
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

        // ===================== 3-WAY MATCH =====================
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

        // ===================== CREATE PAYMENT =====================
        public async Task<int> CreatePaymentAsync(ApPaymentCreateDto dto, int userId)
        {
            const string lastNoSql = @"
SELECT TOP 1 PaymentNo
FROM dbo.SupplierPayment
WHERE PaymentNo IS NOT NULL
ORDER BY Id DESC;";

            const string insertSql = @"
INSERT INTO dbo.SupplierPayment
(
    PaymentNo,
    SupplierInvoiceId,
    SupplierId,
    PaymentDate,
    PaymentMethodId,
    ReferenceNo,
    Amount,
    Notes,
    Status,
    IsActive,
    CreatedBy,
    CreatedDate
)
VALUES
(
    @PaymentNo,
    @SupplierInvoiceId,
    @SupplierId,
    @PaymentDate,
    @PaymentMethodId,
    @ReferenceNo,
    @Amount,
    @Notes,
    1,              -- Posted
    1,
    @UserId,
    SYSDATETIME()
);

SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using var conn = Connection;

            // ----- Get last payment no -----
            var lastNo = await conn.QueryFirstOrDefaultAsync<string>(lastNoSql);

            int nextSeq = 1;

            if (!string.IsNullOrWhiteSpace(lastNo))
            {
                // take only digits from the string (e.g. PY-000123 -> 000123)
                var digits = new string(lastNo.Where(char.IsDigit).ToArray());

                if (int.TryParse(digits, out var n))
                {
                    nextSeq = n + 1;
                }
            }

            // Format as PY-000001, PY-000002, ...
            var paymentNo = $"PY-{nextSeq:000000}";

            // ----- Insert new row -----
            var id = await conn.ExecuteScalarAsync<int>(insertSql, new
            {
                PaymentNo = paymentNo,
                dto.SupplierInvoiceId,
                dto.SupplierId,
                dto.PaymentDate,
                dto.PaymentMethodId,
                dto.ReferenceNo,
                dto.Amount,
                dto.Notes,
                UserId = userId
            });

            return id;
        }



        // ===================== PAYMENTS LIST =====================
        public async Task<IEnumerable<ApPaymentListDto>> GetPaymentsAsync()
        {
            const string sql = @"
SELECT
    sp.Id,
    sp.PaymentNo,
    s.Name        AS SupplierName,
    si.InvoiceNo  AS InvoiceNo,
    sp.PaymentDate,
    sp.PaymentMethodId,
    CASE sp.PaymentMethodId
        WHEN 1 THEN 'Cash'
        WHEN 2 THEN 'Bank Transfer'
        WHEN 3 THEN 'Cheque'
        ELSE 'Other'
    END AS PaymentMethodName,
    sp.Amount,
    sp.ReferenceNo,
    sp.Notes
FROM dbo.SupplierPayment sp
LEFT JOIN dbo.Suppliers        s  ON s.Id  = sp.SupplierId
LEFT JOIN dbo.SupplierInvoicePin si ON si.Id = sp.SupplierInvoiceId
WHERE sp.IsActive = 1
ORDER BY sp.PaymentDate DESC, sp.Id DESC;";

            using var conn = Connection;
            return await conn.QueryAsync<ApPaymentListDto>(sql);
        }

        public async Task<IEnumerable<BankAccountDTO>> GetAllAsync()
        {
            const string sql = @"SELECT * FROM vwBankAccountsBalance ORDER BY BankName";
            return await Connection.QueryAsync<BankAccountDTO>(sql);
        }

        public async Task<BankAccountDTO?> GetByIdAsync(int bankId)
        {
            const string sql = @"SELECT * FROM vwBankAccountsBalance WHERE BankId = @bankId";
            return await Connection.QueryFirstOrDefaultAsync<BankAccountDTO>(sql, new { bankId });
        }
        public async Task<int> UpdateBankBalance(int bankHeadId, decimal newBalance)
        {
            const string sql = "EXEC dbo.sp_UpdateBankAvailableBalance @BankHeadId, @NewBalance;";

            return await Connection.ExecuteAsync(sql, new
            {
                BankHeadId = bankHeadId,
                NewBalance = newBalance
            });
        }
        public async Task<IEnumerable<ApSupplierAdvanceDto>> GetSupplierAdvancesAsync()
        {
            const string sql = @"
SELECT
    a.Id                             AS Id,
    a.AdvanceNo                      AS AdvanceNo,
    a.SupplierId                     AS SupplierId,
    s.SupplierName                   AS SupplierName,
    a.AdvanceDate                    AS AdvanceDate,
    a.Amount                         AS OriginalAmount,
    ISNULL(SUM(adj.AdjustAmount),0)  AS UtilisedAmount,
    a.Amount - ISNULL(SUM(adj.AdjustAmount),0) AS BalanceAmount
FROM dbo.SupplierAdvance a
INNER JOIN dbo.Suppliers s
    ON s.Id = a.SupplierId
LEFT JOIN dbo.SupplierAdvanceAdjust adj
    ON adj.AdvanceId = a.Id
WHERE a.IsActive = 1
GROUP BY
    a.Id,
    a.AdvanceNo,
    a.SupplierId,
    s.SupplierName,
    a.AdvanceDate,
    a.Amount
ORDER BY a.AdvanceDate DESC;";

            return await Connection.QueryAsync<ApSupplierAdvanceDto>(
                sql,
                commandType: CommandType.Text
            );
        }
        public async Task<int> CreateSupplierAdvanceAsync(int userId, ApSupplierAdvanceCreateRequest req)
        {
            // 1) Insert header WITHOUT AdvanceNo
            const string insertSql = @"
INSERT INTO dbo.ApSupplierAdvance
(
    SupplierId,
    AdvanceDate,
    Amount,
    MethodId,
    BankHeadId,
    ReferenceNo,
    Notes,
    CreatedBy,
    CreatedDate,
    UpdatedBy,
    UpdatedDate,
    IsActive
)
OUTPUT INSERTED.Id
VALUES
(
    @SupplierId,
    @AdvanceDate,
    @Amount,
    @MethodId,
    @BankHeadId,
    @ReferenceNo,
    @Notes,
    @UserId,
    SYSUTCDATETIME(),
    @UserId,
    SYSUTCDATETIME(),
    1
);";

            var newId = await Connection.ExecuteScalarAsync<int>(
                insertSql,
                new
                {
                    req.SupplierId,
                    req.AdvanceDate,
                    req.Amount,
                    req.MethodId,
                    req.BankHeadId,
                    req.ReferenceNo,
                    req.Notes,
                    UserId = userId
                }
            );

            // 2) Generate AdvanceNo like ADV-000001
            const string updateNoSql = @"
UPDATE dbo.ApSupplierAdvance
SET AdvanceNo = CONCAT('SUPADV-', RIGHT(CONVERT(VARCHAR(8), @Id + 100000), 6))
WHERE Id = @Id;";

            await Connection.ExecuteAsync(updateNoSql, new { Id = newId });

            // OPTIONAL: 3) Post to GlTransaction (DR advance, CR bank/cash)
            // You can uncomment & adjust if you are ready to post GL here.
            /*
            const string glSql = @"
INSERT INTO dbo.GlTransaction
(
    AccountId,
    TxnDate,
    CurrencyId,
    AmountFC,
    AmountBase
)
SELECT
    -- TODO: replace with correct COA for 'Advance to Supplier'
    s.BudgetLineId AS AccountId,
    @AdvanceDate   AS TxnDate,
    1              AS CurrencyId,
    @Amount        AS AmountFC,
    @Amount        AS AmountBase     -- DR Advance
FROM dbo.Suppliers s
WHERE s.Id = @SupplierId
  AND s.BudgetLineId IS NOT NULL

UNION ALL

SELECT
    @BankHeadId    AS AccountId,
    @AdvanceDate   AS TxnDate,
    1              AS CurrencyId,
    @Amount        AS AmountFC,
    -@Amount       AS AmountBase     -- CR Bank/Cash
WHERE @BankHeadId IS NOT NULL;
";

            await Connection.ExecuteAsync(
                glSql,
                new
                {
                    req.SupplierId,
                    req.AdvanceDate,
                    req.Amount,
                    req.BankHeadId
                }
            );
            */

            return newId;
        }
    }
}



