using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.ModelDTO.TB;

namespace FinanceApi.Repositories
{
    public class FinanceReportRepository : DynamicRepository, IFinanceReportRepository
    {
        public FinanceReportRepository(IDbConnectionFactory factory)
            : base(factory)
        {
        }

        // ============================================================
        //  TRIAL BALANCE SUMMARY  (Opening / Closing, Hierarchical)
        // ============================================================
        // ============================================================
        //  TRIAL BALANCE SUMMARY  (Opening / Closing, Hierarchical)
        // ============================================================
        public async Task<IEnumerable<TrialBalanceDTO>> GetTrialBalanceAsync(ReportBaseDTO dto)
        {
            const string sql = @"
;WITH
------------------------------------------------------------
-- 0) BANK HEAD (used for receipts & payments)
------------------------------------------------------------
BankHead AS (
    SELECT TOP (1) Id AS BankHeadId
    FROM dbo.ChartOfAccount
    WHERE IsActive = 1
      AND (HeadName = 'Bank Accounts' OR HeadCode = '10103')
),

------------------------------------------------------------
-- 1) VIRTUAL GL LINES (ALL MODULES)
------------------------------------------------------------
GlLines AS (
    --------------------------------------------------------
    -- A) Manual Journal
    --------------------------------------------------------
    SELECT
        mj.JournalDate                        AS TransDate,
        'MJ'                                  AS SourceType,
        mj.JournalNo                          AS SourceNo,
        mj.Description                        AS Description,
        mj.AccountId                          AS HeadId,
        ISNULL(TRY_CONVERT(decimal(18,2), mj.Debit),  0) AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), mj.Credit), 0) AS Credit
    FROM dbo.ManualJournal mj
    WHERE mj.IsActive   = 1
      AND mj.isPosted   = 1
      AND mj.JournalDate BETWEEN '1900-01-01' AND ISNULL(@ToDate, '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- B) Sales Invoice lines  (Revenue / Item side)
    --------------------------------------------------------
    SELECT
        si.InvoiceDate                        AS TransDate,
        'SI'                                  AS SourceType,
        si.InvoiceNo                          AS SourceNo,
        ISNULL(sil.Description,'')            AS Description,
        coa.Id                                AS HeadId,
        CAST(0 AS decimal(18,2))              AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), sil.LineAmount),0)
      + ISNULL(TRY_CONVERT(decimal(18,2), sil.Tax),0)      AS Credit
    FROM dbo.SalesInvoiceLine sil
    INNER JOIN dbo.SalesInvoice si
        ON si.Id = sil.SiId AND si.IsActive = 1
    INNER JOIN dbo.Item i
        ON i.Id = sil.ItemId AND i.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = i.BudgetLineId AND coa.IsActive = 1
    WHERE si.InvoiceDate BETWEEN '1900-01-01' AND ISNULL(@ToDate, '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- C) Credit Note lines  (Reverse revenue / item)
    --------------------------------------------------------
    SELECT
        cn.CreditNoteDate                     AS TransDate,
        'CN'                                  AS SourceType,
        cn.CreditNoteNo                       AS SourceNo,
        'Credit Note ' + ISNULL(cn.SiNumber,'') AS Description,
        coa.Id                                AS HeadId,
        ISNULL(TRY_CONVERT(decimal(18,2), cnl.LineNet),0)
      + ISNULL(TRY_CONVERT(decimal(18,2), cnl.Tax),0)      AS Debit,
        CAST(0 AS decimal(18,2))              AS Credit
    FROM dbo.CreditNoteLine cnl
    INNER JOIN dbo.CreditNote cn
        ON cn.Id = cnl.CreditNoteId AND cn.IsActive = 1
    INNER JOIN dbo.Item i
        ON i.Id = cnl.ItemId AND i.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = i.BudgetLineId AND coa.IsActive = 1
    WHERE cn.CreditNoteDate BETWEEN '1900-01-01' AND ISNULL(@ToDate, '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- D) AR – Invoice  (Debit customer AR head)
    --------------------------------------------------------
    SELECT
        si.InvoiceDate                        AS TransDate,
        'ARINV'                               AS SourceType,
        si.InvoiceNo                          AS SourceNo,
        'AR From Invoice ' + si.InvoiceNo     AS Description,
        c.BudgetLineId                        AS HeadId,
        ISNULL(TRY_CONVERT(decimal(18,2), si.Subtotal),0) AS Debit,
        CAST(0 AS decimal(18,2))              AS Credit
    FROM dbo.SalesInvoice si
    LEFT JOIN dbo.DeliveryOrder d
        ON d.Id = si.DoId
    INNER JOIN dbo.SalesOrder so
        ON so.Id = COALESCE(si.SoId, d.SoId)
    INNER JOIN dbo.Customer c
        ON c.Id = so.CustomerId AND c.IsActive = 1
    WHERE si.IsActive = 1
      AND si.InvoiceDate BETWEEN '1900-01-01' AND ISNULL(@ToDate, '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- E) AR – Receipt  (Credit customer AR head)
    --------------------------------------------------------
    SELECT
        r.ReceiptDate                         AS TransDate,
        'ARREC'                               AS SourceType,
        r.ReceiptNo                           AS SourceNo,
        'AR Receipt'                          AS Description,
        c.BudgetLineId                        AS HeadId,
        CAST(0 AS decimal(18,2))              AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), r.AmountReceived),0) AS Credit
    FROM dbo.ArReceipt r
    INNER JOIN dbo.Customer c
        ON c.Id = r.CustomerId AND c.IsActive = 1
    WHERE r.IsActive = 1
      AND r.ReceiptDate BETWEEN '1900-01-01' AND ISNULL(@ToDate, '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- E2) AR – Receipt : BANK / CASH (Debit bank)
    --------------------------------------------------------
    SELECT
        r.ReceiptDate                         AS TransDate,
        'ARRECBNK'                            AS SourceType,
        r.ReceiptNo                           AS SourceNo,
        'AR Receipt - Bank'                   AS Description,
        (SELECT BankHeadId FROM BankHead)     AS HeadId,
        ISNULL(TRY_CONVERT(decimal(18,2), r.AmountReceived),0) AS Debit,
        CAST(0 AS decimal(18,2))              AS Credit
    FROM dbo.ArReceipt r
    WHERE r.IsActive = 1
      AND r.ReceiptDate BETWEEN '1900-01-01' AND ISNULL(@ToDate, '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- F) AR – Credit Note (Credit customer AR head)
    --------------------------------------------------------
    SELECT
        cn.CreditNoteDate                     AS TransDate,
        'ARCN'                                AS SourceType,
        cn.CreditNoteNo                       AS SourceNo,
        'AR Credit Note'                      AS Description,
        c.BudgetLineId                        AS HeadId,
        CAST(0 AS decimal(18,2))              AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), cn.Subtotal),0) AS Credit
    FROM dbo.CreditNote cn
    INNER JOIN dbo.Customer c
        ON c.Id = cn.CustomerId AND c.IsActive = 1
    WHERE cn.IsActive = 1
      AND cn.CreditNoteDate BETWEEN '1900-01-01' AND ISNULL(@ToDate, '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- G1) AP – PIN LINES -> Inventory/Expense (from LinesJson)
    --------------------------------------------------------
    SELECT
        si.InvoiceDate                        AS TransDate,
        'PINL'                                AS SourceType,
        si.InvoiceNo                          AS SourceNo,
        'PIN Line - ' + ISNULL(i.ItemName,'') AS Description,
        coa.Id                                AS HeadId,
        ISNULL(j.LineTotal,0)                 AS Debit,
        CAST(0 AS decimal(18,2))              AS Credit
    FROM dbo.SupplierInvoicePin si
    CROSS APPLY OPENJSON(si.LinesJson) WITH (
        ItemCode  nvarchar(50)  '$.item',
        LineTotal decimal(18,2) '$.lineTotal'
    ) j
    INNER JOIN dbo.Item i
        ON i.ItemCode = j.ItemCode
       AND i.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = i.BudgetLineId
       AND coa.IsActive = 1
    WHERE si.IsActive = 1
      AND si.Status   = 3
      AND si.InvoiceDate BETWEEN '1900-01-01'
                              AND ISNULL(@ToDate, '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- G2) AP – PIN HEADER -> Supplier AP head (Credit)
    --------------------------------------------------------
    SELECT
        si.InvoiceDate                        AS TransDate,
        'PIN'                                 AS SourceType,
        si.InvoiceNo                          AS SourceNo,
        'Supplier Invoice'                    AS Description,
        s.BudgetLineId                        AS HeadId,
        CAST(0 AS decimal(18,2))              AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), si.Amount),0)
      + ISNULL(TRY_CONVERT(decimal(18,2), si.Tax),0)      AS Credit
    FROM dbo.SupplierInvoicePin si
    INNER JOIN dbo.Suppliers s
        ON s.Id = si.SupplierId AND s.IsActive = 1
    WHERE si.IsActive = 1
      AND si.Status   = 3
      AND si.InvoiceDate BETWEEN '1900-01-01' AND ISNULL(@ToDate, '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- H) AP – Supplier Payment (Debit AP)
    --------------------------------------------------------
    SELECT
        sp.PaymentDate                        AS TransDate,
        'SPAY'                                AS SourceType,
        sp.PaymentNo                          AS SourceNo,
        'Supplier Payment'                    AS Description,
        s.BudgetLineId                        AS HeadId,
        ISNULL(TRY_CONVERT(decimal(18,2), sp.Amount),0) AS Debit,
        CAST(0 AS decimal(18,2))              AS Credit
    FROM dbo.SupplierPayment sp
    INNER JOIN dbo.Suppliers s
        ON s.Id = sp.SupplierId AND s.IsActive = 1
    WHERE sp.IsActive = 1
      AND sp.Status   = 1
      AND sp.PaymentDate BETWEEN '1900-01-01' AND ISNULL(@ToDate, '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- H2) AP – Supplier Payment : BANK / CASH (Credit bank)
    --------------------------------------------------------
    SELECT
        sp.PaymentDate                        AS TransDate,
        'SPAYBNK'                             AS SourceType,
        sp.PaymentNo                          AS SourceNo,
        'Supplier Payment - Bank'             AS Description,
        (SELECT BankHeadId FROM BankHead)     AS HeadId,
        CAST(0 AS decimal(18,2))              AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), sp.Amount),0) AS Credit
    FROM dbo.SupplierPayment sp
    WHERE sp.IsActive = 1
      AND sp.Status   = 1
      AND sp.PaymentDate BETWEEN '1900-01-01' AND ISNULL(@ToDate, '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- I) AP – Supplier Debit Note (Debit AP)
    --------------------------------------------------------
    SELECT
        dn.NoteDate                           AS TransDate,
        'SDN'                                 AS SourceType,
        dn.DebitNoteNo                        AS SourceNo,
        ISNULL(dn.Reason,'Supplier Debit Note') AS Description,
        s.BudgetLineId                        AS HeadId,
        ISNULL(ABS(TRY_CONVERT(decimal(18,2), dn.Amount)),0) AS Debit,
        CAST(0 AS decimal(18,2))              AS Credit
    FROM dbo.SupplierDebitNote dn
    INNER JOIN dbo.Suppliers s
        ON s.Id = dn.SupplierId AND s.IsActive = 1
    WHERE dn.IsActive = 1
      AND dn.Status   = 2
      AND dn.NoteDate BETWEEN '1900-01-01' AND ISNULL(@ToDate, '9999-12-31')
),

------------------------------------------------------------
-- 2) MOVEMENTS PER LEAF ACCOUNT
------------------------------------------------------------
Movements AS (
    SELECT
        coa.Id          AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        coa.HeadType,
        coa.ParentHead,
        OpeningMov = SUM(
            CASE 
                WHEN gl.TransDate <  ISNULL(@FromDate, '1900-01-01')
                    THEN ISNULL(gl.Debit,0) - ISNULL(gl.Credit,0)
                ELSE 0
            END
        ),
        PeriodMov = SUM(
            CASE 
                WHEN gl.TransDate >= ISNULL(@FromDate, '1900-01-01')
                 AND gl.TransDate <= ISNULL(@ToDate,   '9999-12-31')
                    THEN ISNULL(gl.Debit,0) - ISNULL(gl.Credit,0)
                ELSE 0
            END
        ),
        -- Opening balance column removed from ChartOfAccount,
        -- treat base opening as 0 for all accounts
        OpeningBalanceBase = CAST(0 AS decimal(18,2))
    FROM dbo.ChartOfAccount coa
    LEFT JOIN GlLines gl
        ON gl.HeadId = coa.Id
    WHERE coa.IsActive = 1
    GROUP BY
        coa.Id,
        coa.HeadCode,
        coa.HeadName,
        coa.HeadType,
        coa.ParentHead
),

------------------------------------------------------------
-- 3) SIGNED OPENING / CLOSING BALANCE (per account)
------------------------------------------------------------
SignedRaw AS (
    SELECT
        HeadId,
        HeadCode,
        HeadName,
        HeadType,
        ParentHead,
        OpeningBalanceBase,
        OpeningMov,
        PeriodMov,
       -- Opening = OpeningBalanceBase + movements BEFORE FromDate
        OpeningSigned_raw =
            OpeningBalanceBase + OpeningMov,

        -- Closing = Opening + movements IN period
        ClosingSigned_raw  =
            OpeningBalanceBase + OpeningMov + PeriodMov
    FROM Movements
),

------------------------------------------------------------
-- 4) ROLLUP BY HEADCODE PREFIX (parent = all codes starting with its code)
------------------------------------------------------------
Aggregated AS (
    SELECT
        p.HeadId,
        p.HeadCode,
        p.HeadName,
        p.HeadType,
        p.ParentHead,   -- this is parent HEAD CODE

        OpeningSigned = SUM(ISNULL(c.OpeningSigned_raw,0)),
        ClosingSigned = SUM(ISNULL(c.ClosingSigned_raw,0))
    FROM SignedRaw p
    JOIN SignedRaw c
      ON CAST(c.HeadCode AS varchar(50))
         LIKE CAST(p.HeadCode AS varchar(50)) + '%'   -- 🔴 Cast both sides
    GROUP BY
        p.HeadId,
        p.HeadCode,
        p.HeadName,
        p.HeadType,
        p.ParentHead
)



SELECT
    HeadId,
    HeadCode,
    HeadName,
    ParentHead,

    OpeningDebit  = CASE WHEN OpeningSigned > 0 THEN OpeningSigned ELSE 0 END,
    OpeningCredit = CASE WHEN OpeningSigned < 0 THEN -OpeningSigned ELSE 0 END,
    ClosingDebit  = CASE WHEN ClosingSigned > 0 THEN ClosingSigned ELSE 0 END,
    ClosingCredit = CASE WHEN ClosingSigned < 0 THEN -ClosingSigned ELSE 0 END
FROM Aggregated
WHERE
    -- keep all heads that have any balance
    (OpeningSigned <> 0 OR ClosingSigned <> 0)
    -- OR always keep root heads (Assets, Liabilities, Equity, Income, Expense)
    OR ParentHead IS NULL
    OR ParentHead = 0
ORDER BY HeadCode;

";

            return await Connection.QueryAsync<TrialBalanceDTO>(
                sql,
                new
                {
                    dto.FromDate,
                    dto.ToDate,
                    dto.CompanyId
                });
        }


        // ============================================================
        //  TRIAL BALANCE DETAIL  (includes bank legs & PINL)
        // ============================================================
        public async Task<IEnumerable<TrialBalanceDetailDTO>> GetTrialBalanceDetailAsync(
            TrialBalanceDetailRequestDTO dto)
        {
            const string sql = @"
;WITH
------------------------------------------------------------
-- 0) BANK HEAD (same as summary)
------------------------------------------------------------
BankHead AS (
    SELECT TOP (1) Id AS BankHeadId
    FROM dbo.ChartOfAccount
    WHERE IsActive = 1
      AND (HeadName = 'Bank Accounts' OR HeadCode = '10103')
),

GlLines AS (
    --------------------------------------------------------
    -- A) Manual Journal
    --------------------------------------------------------
    SELECT
        mj.JournalDate AS TransDate,
        'MJ'           AS SourceType,
        mj.JournalNo   AS SourceNo,
        mj.Description AS Description,
        mj.AccountId   AS HeadId,
        ISNULL(TRY_CONVERT(decimal(18,2), mj.Debit),  0) AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), mj.Credit), 0) AS Credit
    FROM dbo.ManualJournal mj
    WHERE mj.IsActive   = 1
      AND mj.isPosted   = 1
      AND mj.JournalDate BETWEEN ISNULL(@FromDate, '1900-01-01')
                             AND ISNULL(@ToDate,   '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- B) SI lines (Revenue / item)
    --------------------------------------------------------
    SELECT
        si.InvoiceDate AS TransDate,
        'SI'           AS SourceType,
        si.InvoiceNo   AS SourceNo,
        ISNULL(sil.Description,'') AS Description,
        coa.Id         AS HeadId,
        CAST(0 AS decimal(18,2)) AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), sil.LineAmount),0)
      + ISNULL(TRY_CONVERT(decimal(18,2), sil.Tax),0)     AS Credit
    FROM dbo.SalesInvoiceLine sil
    INNER JOIN dbo.SalesInvoice si
        ON si.Id = sil.SiId AND si.IsActive = 1
    INNER JOIN dbo.Item i
        ON i.Id = sil.ItemId AND i.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = i.BudgetLineId AND coa.IsActive = 1
    WHERE si.InvoiceDate BETWEEN ISNULL(@FromDate, '1900-01-01')
                             AND ISNULL(@ToDate,   '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- C) CN lines (reverse revenue / item)
    --------------------------------------------------------
    SELECT
        cn.CreditNoteDate AS TransDate,
        'CN'              AS SourceType,
        cn.CreditNoteNo   AS SourceNo,
        'Credit Note ' + ISNULL(cn.SiNumber,'') AS Description,
        coa.Id            AS HeadId,
        ISNULL(TRY_CONVERT(decimal(18,2), cnl.LineNet),0)
      + ISNULL(TRY_CONVERT(decimal(18,2), cnl.Tax),0)     AS Debit,
        CAST(0 AS decimal(18,2)) AS Credit
    FROM dbo.CreditNoteLine cnl
    INNER JOIN dbo.CreditNote cn
        ON cn.Id = cnl.CreditNoteId AND cn.IsActive = 1
    INNER JOIN dbo.Item i
        ON i.Id = cnl.ItemId AND i.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = i.BudgetLineId AND coa.IsActive = 1
    WHERE cn.CreditNoteDate BETWEEN ISNULL(@FromDate, '1900-01-01')
                                AND ISNULL(@ToDate,   '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- D) AR – Invoice
    --------------------------------------------------------
    SELECT
        si.InvoiceDate AS TransDate,
        'ARINV'        AS SourceType,
        si.InvoiceNo   AS SourceNo,
        'AR From Invoice ' + si.InvoiceNo AS Description,
        c.BudgetLineId AS HeadId,
        ISNULL(TRY_CONVERT(decimal(18,2), si.Subtotal),0) AS Debit,
        CAST(0 AS decimal(18,2)) AS Credit
    FROM dbo.SalesInvoice si
    LEFT JOIN dbo.DeliveryOrder d
        ON d.Id = si.DoId
    INNER JOIN dbo.SalesOrder so
        ON so.Id = COALESCE(si.SoId, d.SoId)
    INNER JOIN dbo.Customer c
        ON c.Id = so.CustomerId
       AND c.IsActive = 1
    WHERE si.IsActive = 1
      AND si.InvoiceDate BETWEEN ISNULL(@FromDate, '1900-01-01')
                             AND ISNULL(@ToDate,   '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- E) AR – Receipt  (Credit AR)
    --------------------------------------------------------
    SELECT
        r.ReceiptDate AS TransDate,
        'ARREC'       AS SourceType,
        r.ReceiptNo   AS SourceNo,
        'AR Receipt'  AS Description,
        c.BudgetLineId AS HeadId,
        CAST(0 AS decimal(18,2)) AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), r.AmountReceived),0) AS Credit
    FROM dbo.ArReceipt r
    INNER JOIN dbo.Customer c
        ON c.Id = r.CustomerId AND c.IsActive = 1
    WHERE r.IsActive = 1
      AND r.ReceiptDate BETWEEN ISNULL(@FromDate, '1900-01-01')
                            AND ISNULL(@ToDate,   '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- E2) AR – Receipt : BANK / CASH (Debit bank)
    --------------------------------------------------------
    SELECT
        r.ReceiptDate AS TransDate,
        'ARRECBNK'    AS SourceType,
        r.ReceiptNo   AS SourceNo,
        'AR Receipt - Bank' AS Description,
        (SELECT BankHeadId FROM BankHead) AS HeadId,
        ISNULL(TRY_CONVERT(decimal(18,2), r.AmountReceived),0) AS Debit,
        CAST(0 AS decimal(18,2)) AS Credit
    FROM dbo.ArReceipt r
    WHERE r.IsActive = 1
      AND r.ReceiptDate BETWEEN ISNULL(@FromDate, '1900-01-01')
                            AND ISNULL(@ToDate,   '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- F) AR – Credit Note
    --------------------------------------------------------
    SELECT
        cn.CreditNoteDate AS TransDate,
        'ARCN'            AS SourceType,
        cn.CreditNoteNo   AS SourceNo,
        'AR Credit Note'  AS Description,
        c.BudgetLineId    AS HeadId,
        CAST(0 AS decimal(18,2)) AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), cn.Subtotal),0) AS Credit
    FROM dbo.CreditNote cn
    INNER JOIN dbo.Customer c
        ON c.Id = cn.CustomerId AND c.IsActive = 1
    WHERE cn.IsActive = 1
      AND cn.CreditNoteDate BETWEEN ISNULL(@FromDate, '1900-01-01')
                                AND ISNULL(@ToDate,   '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- G1) AP – Supplier Invoice LINES (PINL -> item heads)
    --------------------------------------------------------
    SELECT
        si.InvoiceDate AS TransDate,
        'PINL'         AS SourceType,
        si.InvoiceNo   AS SourceNo,
        'PIN Line - ' + ISNULL(i.ItemName,'') AS Description,
        coa.Id         AS HeadId,
        ISNULL(j.LineTotal,0) AS Debit,
        CAST(0 AS decimal(18,2)) AS Credit
    FROM dbo.SupplierInvoicePin si
    CROSS APPLY OPENJSON(si.LinesJson) WITH (
        ItemCode  nvarchar(50)  '$.item',
        LineTotal decimal(18,2) '$.lineTotal'
    ) j
    INNER JOIN dbo.Item i
        ON i.ItemCode = j.ItemCode
       AND i.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = i.BudgetLineId
       AND coa.IsActive = 1
    WHERE si.IsActive = 1
      AND si.Status   = 3
      AND si.InvoiceDate BETWEEN ISNULL(@FromDate, '1900-01-01')
                             AND ISNULL(@ToDate,   '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- G2) AP – Supplier Invoice (PIN -> supplier head)
    --------------------------------------------------------
    SELECT
        si.InvoiceDate AS TransDate,
        'PIN'          AS SourceType,
        si.InvoiceNo   AS SourceNo,
        'Supplier Invoice' AS Description,
        s.BudgetLineId AS HeadId,
        CAST(0 AS decimal(18,2)) AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), si.Amount),0)
      + ISNULL(TRY_CONVERT(decimal(18,2), si.Tax),0)     AS Credit
    FROM dbo.SupplierInvoicePin si
    INNER JOIN dbo.Suppliers s
        ON s.Id = si.SupplierId AND s.IsActive = 1
    WHERE si.IsActive = 1
      AND si.Status   = 3
      AND si.InvoiceDate BETWEEN ISNULL(@FromDate, '1900-01-01')
                             AND ISNULL(@ToDate,   '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- H) AP – Supplier Payment  (Debit AP)
    --------------------------------------------------------
    SELECT
        sp.PaymentDate AS TransDate,
        'SPAY'         AS SourceType,
        sp.PaymentNo   AS SourceNo,
        'Supplier Payment' AS Description,
        s.BudgetLineId AS HeadId,
        ISNULL(TRY_CONVERT(decimal(18,2), sp.Amount),0) AS Debit,
        CAST(0 AS decimal(18,2)) AS Credit
    FROM dbo.SupplierPayment sp
    INNER JOIN dbo.Suppliers s
        ON s.Id = sp.SupplierId AND s.IsActive = 1
    WHERE sp.IsActive = 1
      AND sp.Status   = 1
      AND sp.PaymentDate BETWEEN ISNULL(@FromDate, '1900-01-01')
                             AND ISNULL(@ToDate,   '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- H2) AP – Supplier Payment : BANK / CASH (Credit bank)
    --------------------------------------------------------
    SELECT
        sp.PaymentDate AS TransDate,
        'SPAYBNK'      AS SourceType,
        sp.PaymentNo   AS SourceNo,
        'Supplier Payment - Bank' AS Description,
        (SELECT BankHeadId FROM BankHead) AS HeadId,
        CAST(0 AS decimal(18,2)) AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), sp.Amount),0) AS Credit
    FROM dbo.SupplierPayment sp
    WHERE sp.IsActive = 1
      AND sp.Status   = 1
      AND sp.PaymentDate BETWEEN ISNULL(@FromDate, '1900-01-01')
                             AND ISNULL(@ToDate,   '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- I) AP – Supplier Debit Note
    --------------------------------------------------------
    SELECT
        dn.NoteDate     AS TransDate,
        'SDN'           AS SourceType,
        dn.DebitNoteNo  AS SourceNo,
        ISNULL(dn.Reason,'Supplier Debit Note') AS Description,
        s.BudgetLineId  AS HeadId,
        ISNULL(ABS(TRY_CONVERT(decimal(18,2), dn.Amount)),0) AS Debit,
        CAST(0 AS decimal(18,2)) AS Credit
    FROM dbo.SupplierDebitNote dn
    INNER JOIN dbo.Suppliers s
        ON s.Id = dn.SupplierId AND s.IsActive = 1
    WHERE dn.IsActive = 1
      AND dn.Status   = 2
      AND dn.NoteDate BETWEEN ISNULL(@FromDate, '1900-01-01')
                          AND ISNULL(@ToDate,   '9999-12-31')
)
SELECT
    TransDate,
    SourceType,
    SourceNo,
    Description,
    Debit,
    Credit
FROM GlLines
WHERE HeadId = @HeadId
ORDER BY TransDate, SourceType, SourceNo;
";

            return await Connection.QueryAsync<TrialBalanceDetailDTO>(
                sql,
                new
                {
                    dto.HeadId,
                    dto.FromDate,
                    dto.ToDate,
                    dto.CompanyId
                });
        }

        public async Task<IEnumerable<ProfitLossViewInfo>> GetProfitLossDetails()
        {
            const string sql = @"
DECLARE @FromDate date = '2025-04-01';
DECLARE @ToDate   date = '2025-04-30';

;WITH
------------------------------------------------------------
-- 1) INCOME / EXPENSE HEADS (leaf or all – up to you)
------------------------------------------------------------
CoaIE AS (
    SELECT
        c.Id        AS HeadId,
        c.HeadCode,
        c.HeadName,
        c.HeadType
    FROM Finance.dbo.ChartOfAccount c
    WHERE c.IsActive = 1
      AND c.HeadType IN ('I','E')
      -- AND c.HeadLevel = 3      -- uncomment if you only want level 3
),

------------------------------------------------------------
-- 2) UNIFIED GL FROM:
--      a) GlTransaction (AmountBase only)
--      b) ManualJournal (Debit/Credit columns)
------------------------------------------------------------
AllGl AS (
    -- a) GlTransaction  --> split AmountBase into Debit / Credit
    SELECT
        gt.AccountId,
        gt.TxnDate      AS TransDate,
        Debit  = CASE WHEN gt.AmountBase > 0 THEN gt.AmountBase ELSE 0 END,
        Credit = CASE WHEN gt.AmountBase < 0 THEN -gt.AmountBase ELSE 0 END
    FROM Finance.dbo.GlTransaction gt
    WHERE gt.TxnDate BETWEEN @FromDate AND @ToDate

    UNION ALL

    -- b) ManualJournal (only posted & active)
    SELECT
        mj.AccountId,
        mj.JournalDate  AS TransDate,
        Debit  = ISNULL(mj.Debit,  0.00),
        Credit = ISNULL(mj.Credit, 0.00)
    FROM Finance.dbo.ManualJournal mj
    WHERE mj.IsActive = 1
      AND mj.isPosted = 1
      AND mj.JournalDate BETWEEN @FromDate AND @ToDate
),

------------------------------------------------------------
-- 3) SUM BY ACCOUNT (COA HEAD)
------------------------------------------------------------
AggByHead AS (
    SELECT
        c.HeadId,
        Sales =
            SUM(
                CASE
                    WHEN c.HeadType = 'I' THEN ag.Credit - ag.Debit
                    ELSE 0
                END
            ),
        Purchase =
            SUM(
                CASE
                    WHEN c.HeadType = 'E' THEN ag.Debit - ag.Credit
                    ELSE 0
                END
            )
    FROM AllGl ag
    INNER JOIN CoaIE c
        ON c.HeadId = ag.AccountId
    GROUP BY c.HeadId
),

------------------------------------------------------------
-- 4) FINAL P&L PER HEAD
------------------------------------------------------------
Final AS (
    SELECT
        c.HeadId,
        c.HeadCode,
        c.HeadName,
        c.HeadType,
        Sales    = ISNULL(a.Sales, 0.00),
        Purchase = ISNULL(a.Purchase, 0.00)
    FROM CoaIE c
    LEFT JOIN AggByHead a
        ON a.HeadId = c.HeadId
)

SELECT
    HeadId,
    HeadCode,
    HeadName,
    HeadType,
    Purchase,
    Sales,
    NetProfit = Sales - Purchase
FROM Final
ORDER BY HeadCode;

";

            return await Connection.QueryAsync<ProfitLossViewInfo>(sql);
        }
    
       public async Task<IEnumerable<BalanceSheetViewInfo>> GetBalanceSheetAsync()
        {
            const string sql = @"
WITH
------------------------------------------------------------
-- A) BUILD FULL COA TREE WITH ROOT & FIRST CHILD (GROUP)
------------------------------------------------------------
CoaTree AS (
    -- ROOT NODES (Asset, Liabilities, Income, Expense, Equity...)
    SELECT
        c.Id,
        c.HeadCode,
        c.HeadName,
        c.HeadType,        -- 'A','L','I','E','Q'
        c.ParentHead,
        RootHeadId      = c.Id,
        RootHeadType    = c.HeadType,
        FirstChildId    = CAST(NULL AS INT),
        FirstChildName  = CAST(NULL AS NVARCHAR(200))
    FROM dbo.ChartOfAccount c
    WHERE c.IsActive = 1
      AND (c.ParentHead IS NULL OR c.ParentHead = 0)

    UNION ALL

    -- CHILD NODES
    SELECT
        c.Id,
        c.HeadCode,
        c.HeadName,
        c.HeadType,
        c.ParentHead,
        t.RootHeadId,
        t.RootHeadType,
        FirstChildId = CAST(
            CASE 
                -- parent is ROOT → this row is FIRST CHILD
                WHEN t.ParentHead IS NULL OR t.ParentHead = 0 THEN c.Id
                -- already have first child → propagate
                WHEN t.FirstChildId IS NOT NULL THEN t.FirstChildId
                ELSE c.Id
            END AS INT
        ),
        FirstChildName = CAST(
            CASE 
                WHEN t.ParentHead IS NULL OR t.ParentHead = 0 THEN c.HeadName
                WHEN t.FirstChildName IS NOT NULL THEN t.FirstChildName
                ELSE c.HeadName
            END AS NVARCHAR(200)
        )
    FROM dbo.ChartOfAccount c
    INNER JOIN CoaTree t
        ON c.ParentHead = t.Id
    WHERE c.IsActive = 1
),

------------------------------------------------------------
-- 0) FIND AR / AP CONTROL HEADS (BY NAME)
--    (Still calculated but not used for balance now)
------------------------------------------------------------
ArHead AS (
    SELECT TOP (1) Id AS ArHeadId
    FROM dbo.ChartOfAccount
    WHERE IsActive = 1
      AND LTRIM(RTRIM(HeadName)) LIKE 'Account Receivable%'
),
ApHead AS (
    SELECT TOP (1) Id AS ApHeadId
    FROM dbo.ChartOfAccount
    WHERE IsActive = 1
      AND LTRIM(RTRIM(HeadName)) LIKE 'Account Payable%'
),

------------------------------------------------------------
-- 1) AR CONTROL (Invoices - Receipts - Credit Notes)
------------------------------------------------------------
ArInv AS (
    SELECT
        InvTotal = ISNULL(SUM(TRY_CONVERT(decimal(18,2), v.Amount)), 0)
    FROM dbo.vwSalesInvoiceOpenForReceipt v
),
ArRec AS (
    SELECT
        RecTotal = ISNULL(SUM(ISNULL(ra.AllocatedAmount, 0)), 0)
    FROM dbo.ArReceiptAllocation ra
    WHERE ra.IsActive = 1
),
ArCn AS (
    SELECT
        CnTotal = ISNULL(SUM(
                     ISNULL(TRY_CONVERT(decimal(18,2), cnl.LineNet),0) +
                     ISNULL(TRY_CONVERT(decimal(18,2), cnl.Tax    ),0)
                 ),0)
    FROM dbo.CreditNoteLine cnl
    INNER JOIN dbo.CreditNote cn
        ON cn.Id = cnl.CreditNoteId
    WHERE cn.IsActive  = 1
      AND cnl.IsActive = 1
),

------------------------------------------------------------
-- 2) AP CONTROL (PIN - Payments - Debit Notes)
------------------------------------------------------------
ApInv AS (
    SELECT
        InvTotal = ISNULL(SUM(ISNULL(si.Amount, 0) + ISNULL(si.Tax, 0)), 0)
    FROM dbo.SupplierInvoicePin si
    WHERE si.IsActive = 1
      AND si.Status   = 3
),
ApPay AS (
    SELECT
        PayTotal = ISNULL(SUM(ISNULL(sp.Amount, 0)), 0)
    FROM dbo.SupplierPayment sp
    WHERE sp.IsActive = 1
),
ApDn AS (
    SELECT 
        DnTotal = ISNULL(SUM(
            ISNULL(
                CASE 
                    WHEN dn.Amount < 0 THEN -dn.Amount
                    ELSE dn.Amount
                END, 0)
        ),0)
    FROM dbo.SupplierDebitNote dn
    WHERE dn.IsActive = 1
      AND dn.Status   = 2
),

------------------------------------------------------------
-- 3) GL LINES (ALL MODULES)
------------------------------------------------------------
GlLines AS (

    -- A) Manual Journal
    SELECT
        mj.JournalDate                                    AS TransDate,
        'MJ'                                              AS SourceType,
        mj.Id                                             AS SourceId,
        mj.JournalNo                                      AS SourceNo,
        mj.Description                                    AS Description,
        mj.AccountId                                      AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        ISNULL(TRY_CONVERT(decimal(18,2), mj.Debit),  0)  AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), mj.Credit), 0)  AS Credit
    FROM dbo.ManualJournal mj
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = mj.AccountId
    WHERE mj.IsActive    = 1
      AND mj.isPosted    = 1
      AND mj.IsRecurring = 0

    UNION ALL

    -- B) Sales Invoice – CUSTOMER AR SIDE (Dr Customer)
    SELECT
        si.InvoiceDate                                    AS TransDate,
        'SI-AR'                                           AS SourceType,
        si.Id                                             AS SourceId,
        si.InvoiceNo                                      AS SourceNo,
        'Invoice to ' + ISNULL(c.CustomerName,'')         AS Description,
        c.BudgetLineId                                    AS HeadId,  -- CUSTOMER COA
        coa.HeadCode,
        coa.HeadName,
        ISNULL(TRY_CONVERT(decimal(18,2), si.Total), 0)   AS Debit,
        0                                                 AS Credit
    FROM dbo.SalesInvoice si
    INNER JOIN dbo.SalesOrder so
        ON so.Id = si.SoId
    INNER JOIN dbo.Customer c
        ON c.Id = so.CustomerId
       AND c.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = c.BudgetLineId
       AND coa.IsActive = 1
    WHERE si.IsActive = 1

    UNION ALL

    -- C) AR Receipt Allocation – CUSTOMER AR SIDE (Cr Customer)
    SELECT
        r.ReceiptDate                                     AS TransDate,
        'AR-REC'                                          AS SourceType,
        r.Id                                              AS SourceId,
        r.ReceiptNo                                       AS SourceNo,
        'Receipt from ' + ISNULL(c.CustomerName,'')       AS Description,
        c.BudgetLineId                                    AS HeadId,  -- CUSTOMER COA
        coa.HeadCode,
        coa.HeadName,
        0                                                 AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), ra.AllocatedAmount),0) AS Credit
    FROM dbo.ArReceiptAllocation ra
    INNER JOIN dbo.ArReceipt r
        ON r.Id = ra.ReceiptId
       AND r.IsActive = 1
    INNER JOIN dbo.Customer c
        ON c.Id = r.CustomerId
       AND c.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = c.BudgetLineId
       AND coa.IsActive = 1
    WHERE ra.IsActive = 1

    UNION ALL

    -- D) Credit Note – CUSTOMER AR SIDE (Cr Customer)
    SELECT
        cn.CreditNoteDate                                 AS TransDate,
        'CN-AR'                                           AS SourceType,
        cn.Id                                             AS SourceId,
        cn.CreditNoteNo                                   AS SourceNo,
        'Credit Note to ' + ISNULL(
            COALESCE(c1.CustomerName, c2.CustomerName),'' 
        )                                                 AS Description,
        COALESCE(c1.BudgetLineId, c2.BudgetLineId)        AS HeadId,  -- CUSTOMER COA
        coa.HeadCode,
        coa.HeadName,
        0                                                 AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), cnl.LineNet),0) +
        ISNULL(TRY_CONVERT(decimal(18,2), cnl.Tax    ),0) AS Credit
    FROM dbo.CreditNoteLine cnl
    INNER JOIN dbo.CreditNote cn
        ON cn.Id = cnl.CreditNoteId
       AND cn.IsActive  = 1
       AND cnl.IsActive = 1
    LEFT JOIN dbo.Customer c1
        ON c1.Id = cn.CustomerId
       AND c1.IsActive = 1
    LEFT JOIN dbo.SalesInvoice si_cn
        ON si_cn.Id = cn.SiId
       AND si_cn.IsActive = 1
    LEFT JOIN dbo.SalesOrder so_cn
        ON so_cn.Id = si_cn.SoId
    LEFT JOIN dbo.Customer c2
        ON c2.Id = so_cn.CustomerId
       AND c2.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = COALESCE(c1.BudgetLineId, c2.BudgetLineId)
       AND coa.IsActive = 1

    UNION ALL

    -- E) Sales Invoice lines  (item P&L side)
    SELECT
        si.InvoiceDate                                    AS TransDate,
        'SI'                                              AS SourceType,
        sil.Id                                            AS SourceId,
        si.InvoiceNo                                      AS SourceNo,
        ISNULL(sil.Description, '')                       AS Description,
        i.BudgetLineId                                    AS HeadId,  -- item income/COGS
        coa.HeadCode,
        coa.HeadName,
        0                                                 AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), sil.LineAmount),0) +
        ISNULL(TRY_CONVERT(decimal(18,2), sil.Tax),0)     AS Credit
    FROM dbo.SalesInvoiceLine sil
    INNER JOIN dbo.SalesInvoice si
        ON si.Id = sil.SiId
       AND si.IsActive = 1
    INNER JOIN dbo.Item i
        ON i.Id = sil.ItemId
       AND i.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = i.BudgetLineId
       AND coa.IsActive = 1

    UNION ALL

    -- F) Credit Note lines  (item P&L side)
    SELECT
        cn.CreditNoteDate                                 AS TransDate,
        'CN'                                              AS SourceType,
        cnl.Id                                            AS SourceId,
        cn.CreditNoteNo                                   AS SourceNo,
        'Credit Note for ' + ISNULL(cn.SiNumber,'')       AS Description,
        i.BudgetLineId                                    AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        ISNULL(TRY_CONVERT(decimal(18,2), cnl.LineNet ),0) +
        ISNULL(TRY_CONVERT(decimal(18,2), cnl.Tax     ),0) AS Debit,
        0                                                 AS Credit
    FROM dbo.CreditNoteLine cnl
    INNER JOIN dbo.CreditNote cn
        ON cn.Id = cnl.CreditNoteId
       AND cn.IsActive  = 1
       AND cnl.IsActive = 1
    INNER JOIN dbo.Item i
        ON i.Id = cnl.ItemId
       AND i.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = i.BudgetLineId
       AND coa.IsActive = 1

    UNION ALL

    -- G) Supplier Invoice PIN lines (asset/expense side)
    SELECT
        pin.InvoiceDate                                   AS TransDate,
        'PIN'                                             AS SourceType,
        pin.Id                                            AS SourceId,
        pin.InvoiceNo                                     AS SourceNo,
        'PIN line for item ' + j.item                     AS Description,
        i.BudgetLineId                                    AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        ISNULL(TRY_CONVERT(decimal(18,2), j.lineTotal),0) AS Debit,
        0                                                 AS Credit
    FROM dbo.SupplierInvoicePin pin
    CROSS APPLY OPENJSON(pin.LinesJson)
    WITH (
        item        NVARCHAR(50)  '$.item',
        location    NVARCHAR(100) '$.location',
        qty         DECIMAL(18,4) '$.qty',
        unitPrice   DECIMAL(18,4) '$.unitPrice',
        discountPct DECIMAL(18,4) '$.discountPct',
        lineTotal   DECIMAL(18,2) '$.lineTotal'
    ) AS j
    INNER JOIN dbo.Item i
        ON i.ItemCode = j.item
       AND i.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = i.BudgetLineId
       AND coa.IsActive = 1
    WHERE pin.IsActive = 1
      AND pin.Status   = 3

    UNION ALL

    -- H) Supplier Invoice PIN – AP side (Supplier head)
    SELECT
        pin.InvoiceDate                                   AS TransDate,
        'PIN-AP'                                          AS SourceType,
        pin.Id                                            AS SourceId,
        pin.InvoiceNo                                     AS SourceNo,
        'AP for supplier ' + ISNULL(s.Name,'')            AS Description,
        s.BudgetLineId                                    AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        0                                                 AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), pin.Amount),0) +
        ISNULL(TRY_CONVERT(decimal(18,2), pin.Tax   ),0)  AS Credit
    FROM dbo.SupplierInvoicePin pin
    INNER JOIN dbo.Suppliers s
        ON s.Id = pin.SupplierId
       AND s.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = s.BudgetLineId
       AND coa.IsActive = 1
    WHERE pin.IsActive = 1
      AND pin.Status   = 3

    UNION ALL

    -- I) Supplier Debit Note – AP side (Supplier head)
    SELECT
        dn.NoteDate                                       AS TransDate,
        'DN-AP'                                           AS SourceType,
        dn.Id                                             AS SourceId,
        dn.DebitNoteNo                                    AS SourceNo,
        'Supplier DN ' + ISNULL(s.Name,'')                AS Description,
        s.BudgetLineId                                    AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        ISNULL(
            TRY_CONVERT(decimal(18,2),
                CASE 
                    WHEN dn.Amount < 0 THEN -dn.Amount
                    ELSE dn.Amount
                END
            ),0)                                          AS Debit,
        0                                                 AS Credit
    FROM dbo.SupplierDebitNote dn
    INNER JOIN dbo.Suppliers s
        ON s.Id = dn.SupplierId
       AND s.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = s.BudgetLineId
       AND coa.IsActive = 1
    WHERE dn.IsActive = 1
      AND dn.Status   = 2

    UNION ALL

    -- J) Supplier Payment – AP side (Supplier head)
    SELECT
        sp.PaymentDate                                    AS TransDate,
        'SP-AP'                                           AS SourceType,
        sp.Id                                             AS SourceId,
        sp.PaymentNo                                      AS SourceNo,
        'Payment to supplier ' + ISNULL(s.Name,'')        AS Description,
        s.BudgetLineId                                    AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        ISNULL(TRY_CONVERT(decimal(18,2), sp.Amount),0)   AS Debit,
        0                                                 AS Credit
    FROM dbo.SupplierPayment sp
    INNER JOIN dbo.Suppliers s
        ON s.Id = sp.SupplierId
       AND s.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = s.BudgetLineId
       AND coa.IsActive = 1
    WHERE sp.IsActive = 1
),

------------------------------------------------------------
-- 4) BASE SUMMARY PER HEAD
------------------------------------------------------------
Base AS (
    SELECT
        coa.Id        AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        coa.HeadType,
        ct.RootHeadType,
        ct.FirstChildId,
        ct.FirstChildName,
        ISNULL(coa.ParentHead, 0) AS ParentHead,
        arH.ArHeadId,
        apH.ApHeadId,

        -- Opening only for AR/AP (not used in final balance for parents)
        OpeningBalance =
            CASE 
                WHEN coa.Id = arH.ArHeadId THEN
                    ISNULL(arI.InvTotal, 0)
                WHEN coa.Id = apH.ApHeadId THEN
                    ISNULL(apI.InvTotal, 0)
                ELSE
                    0
            END,

        DebitTotal =
            ISNULL(SUM(ISNULL(gl.Debit,0)),0),

        CreditTotal =
            ISNULL(SUM(ISNULL(gl.Credit,0)),0)
    FROM dbo.ChartOfAccount coa
    INNER JOIN CoaTree ct
        ON ct.Id = coa.Id
    LEFT JOIN GlLines gl ON gl.HeadId = coa.Id
    CROSS JOIN ArHead arH
    CROSS JOIN ApHead apH
    CROSS JOIN ArInv  arI
    CROSS JOIN ArRec  arR
    CROSS JOIN ArCn   arC
    CROSS JOIN ApInv  apI
    CROSS JOIN ApPay  apP
    CROSS JOIN ApDn   apD
    WHERE coa.IsActive = 1
    GROUP BY
        coa.Id,
        coa.HeadCode,
        coa.HeadName,
        coa.HeadType,
        ct.RootHeadType,
        ct.FirstChildId,
        ct.FirstChildName,
        coa.ParentHead,
        arH.ArHeadId,
        apH.ApHeadId,
        arI.InvTotal,
        apI.InvTotal
)

------------------------------------------------------------
-- 5) FINAL RESULT – BALANCE SHEET VIEW
--    Parent heads (including AR/AP) => 0
--    Only leaf heads show amount
--    Liabilities => credit positive
------------------------------------------------------------
SELECT
    Side =
        CASE 
            WHEN RootHeadType = 'L' THEN 'Liabilities'
            WHEN RootHeadType = 'A' THEN 'Assets'
            ELSE NULL
        END,
    GroupHeadId   = FirstChildId,
    GroupHeadName = FirstChildName,
    HeadId,
    HeadCode,
    HeadName,
    ParentHead,

    -- Optionally show opening only for AR/AP (you can set to NULL always)
    CASE 
        WHEN HeadId = ArHeadId OR HeadId = ApHeadId
            THEN OpeningBalance
        ELSE NULL
    END AS OpeningBalance,

    Balance =
        CASE 
            -- 🔹 If this head has children → parent, show 0 (AR/AP included)
            WHEN EXISTS (
                SELECT 1
                FROM dbo.ChartOfAccount cChild
                WHERE cChild.ParentHead = HeadId
                  AND cChild.IsActive   = 1
            ) THEN 0

            -- 🔹 Leaf node: normal calculation
            ELSE
                CASE 
                    WHEN RootHeadType = 'L' THEN
                        -- Liabilities: credit positive
                        -(OpeningBalance + DebitTotal - CreditTotal)
                    ELSE
                        -- Assets: debit positive
                        (OpeningBalance + DebitTotal - CreditTotal)
                END
        END
FROM Base
WHERE RootHeadType IN ('A','L')       -- ONLY ASSET & LIABILITY SIDE
ORDER BY
    CASE 
        WHEN RootHeadType = 'L' THEN 1
        WHEN RootHeadType = 'A' THEN 2
        ELSE 3
    END,
    GroupHeadName,
    HeadCode
OPTION (MAXRECURSION 100);

";

            return await Connection.QueryAsync<BalanceSheetViewInfo>(sql);
        }
        public async Task<IEnumerable<DaybookDTO>> GetDaybookAsync(ReportBaseDTO dto)
        {
            const string sql = @"
;WITH DaybookRaw AS
(
    ------------------------------------------------------------
    -- 1) SALES INVOICE  (Customer AR Debit)
    ------------------------------------------------------------
    SELECT
        si.InvoiceDate        AS TransDate,
        si.InvoiceNo          AS VoucherNo,
        'SI'                  AS VoucherType,
        'Sales Invoice'       AS VoucherName,
        COALESCE(coaSi.HeadName, 'No COA (Item)') AS AccountHeadName,
        si.Remarks            AS Description,
        CAST(ISNULL(si.Total, 0.00) AS DECIMAL(18,2)) AS Debit,
        CAST(0.00 AS DECIMAL(18,2))                   AS Credit
    FROM Finance.dbo.SalesInvoice si
    OUTER APPLY
    (
        SELECT TOP (1) coa.HeadName
        FROM Finance.dbo.SalesInvoiceLine sil
        INNER JOIN dbo.Item itm
            ON itm.Id = sil.ItemId
        INNER JOIN Finance.dbo.ChartOfAccount coa
            ON coa.Id = itm.BudgetLineId
           AND coa.IsActive = 1
        WHERE sil.SiId = si.Id
        ORDER BY coa.HeadName
    ) coaSi
    WHERE si.IsActive = 1
      AND si.InvoiceDate BETWEEN @FromDate AND @ToDate

    UNION ALL

    ------------------------------------------------------------
    -- 1B) CREDIT NOTE (reverse of invoice)
    ------------------------------------------------------------
    SELECT
        cn.CreditNoteDate     AS TransDate,
        cn.CreditNoteNo       AS VoucherNo,
        'CN'                  AS VoucherType,
        'Credit Note'         AS VoucherName,
        COALESCE(coaCn.HeadName, 'No COA (Item)') AS AccountHeadName,
        CONCAT(
            'Credit Note for ',
            ISNULL(cn.CustomerName, ''),
            CASE 
                WHEN cn.SiNumber IS NOT NULL THEN ' (SI: ' + cn.SiNumber + ')' 
                ELSE '' 
            END
        )                    AS Description,
        CAST(0.00 AS DECIMAL(18,2))                    AS Debit,
        CAST(ISNULL(cn.Subtotal, 0.00) AS DECIMAL(18,2)) AS Credit
    FROM Finance.dbo.CreditNote cn
    OUTER APPLY
    (
        SELECT TOP (1) coa.HeadName
        FROM Finance.dbo.CreditNoteLine cnl
        INNER JOIN dbo.Item itm
            ON itm.Id = cnl.ItemId
        INNER JOIN Finance.dbo.ChartOfAccount coa
            ON coa.Id = itm.BudgetLineId
           AND coa.IsActive = 1
        WHERE cnl.CreditNoteId = cn.Id
          AND cnl.IsActive = 1
        ORDER BY coa.HeadName
    ) coaCn
    WHERE cn.IsActive = 1
      AND cn.CreditNoteDate BETWEEN @FromDate AND @ToDate

    UNION ALL

    ------------------------------------------------------------
    -- 2) AR RECEIPT (Customer Receipt – money coming in)
    ------------------------------------------------------------
    SELECT
        r.ReceiptDate         AS TransDate,
        r.ReceiptNo           AS VoucherNo,
        'AR-RCPT'             AS VoucherType,
        'Customer Receipt'    AS VoucherName,
        coa.HeadName          AS AccountHeadName,
        r.Remarks             AS Description,
        CAST(0.00 AS DECIMAL(18,2))                        AS Debit,
        CAST(ISNULL(r.AmountReceived, 0.00) AS DECIMAL(18,2)) AS Credit
    FROM Finance.dbo.ArReceipt r
    INNER JOIN Finance.dbo.Customer c
        ON c.Id = r.CustomerId
       AND c.IsActive = 1
    INNER JOIN Finance.dbo.ChartOfAccount coa
        ON coa.Id = c.BudgetLineId
       AND coa.IsActive = 1
    WHERE r.IsActive = 1
      AND r.ReceiptDate BETWEEN @FromDate AND @ToDate

    UNION ALL

    ------------------------------------------------------------
    -- 3) SUPPLIER INVOICE PIN  (AP – money we owe)
    ------------------------------------------------------------
    SELECT
        pin.InvoiceDate       AS TransDate,
        pin.InvoiceNo         AS VoucherNo,
        'PIN'                 AS VoucherType,
        'Supplier Invoice'    AS VoucherName,
        coa.HeadName          AS AccountHeadName,
        NULL                  AS Description,
        CAST(0.00 AS DECIMAL(18,2)) AS Debit,
        CAST(ISNULL(pin.Amount, 0.00) + ISNULL(pin.Tax, 0.00) AS DECIMAL(18,2)) AS Credit
    FROM Finance.dbo.SupplierInvoicePin pin
    INNER JOIN Finance.dbo.Suppliers s
        ON s.Id = pin.SupplierId
       AND s.IsActive = 1
    INNER JOIN Finance.dbo.ChartOfAccount coa
        ON coa.Id = s.BudgetLineId
       AND coa.IsActive = 1
    WHERE pin.IsActive = 1
      AND pin.InvoiceDate BETWEEN @FromDate AND @ToDate

    UNION ALL

    ------------------------------------------------------------
    -- 4) SUPPLIER PAYMENT (AP Payment – money going out)
    ------------------------------------------------------------
    SELECT
        sp.PaymentDate        AS TransDate,
        sp.PaymentNo          AS VoucherNo,
        'SPAY'                AS VoucherType,
        'Supplier Payment'    AS VoucherName,
        coa.HeadName          AS AccountHeadName,
        sp.Notes              AS Description,
        CAST(ISNULL(sp.Amount, 0.00) AS DECIMAL(18,2)) AS Debit,
        CAST(0.00 AS DECIMAL(18,2))                     AS Credit
    FROM Finance.dbo.SupplierPayment sp
    INNER JOIN Finance.dbo.Suppliers s
        ON s.Id = sp.SupplierId
       AND s.IsActive = 1
    INNER JOIN Finance.dbo.ChartOfAccount coa
        ON coa.Id = s.BudgetLineId
       AND coa.IsActive = 1
    WHERE sp.IsActive = 1
      AND sp.PaymentDate BETWEEN @FromDate AND @ToDate

    UNION ALL

    ------------------------------------------------------------
    -- 5) SUPPLIER DEBIT NOTE  (usually reduces what we owe)
    ------------------------------------------------------------
    SELECT
        dn.NoteDate           AS TransDate,
        dn.DebitNoteNo        AS VoucherNo,
        'SDN'                 AS VoucherType,
        'Supplier Debit Note' AS VoucherName,
        coa.HeadName          AS AccountHeadName,
        dn.Reason             AS Description,
        CAST(ISNULL(dn.Amount, 0.00) AS DECIMAL(18,2)) AS Debit,
        CAST(0.00 AS DECIMAL(18,2))                    AS Credit
    FROM Finance.dbo.SupplierDebitNote dn
    INNER JOIN Finance.dbo.Suppliers s
        ON s.Id = dn.SupplierId
       AND s.IsActive = 1
    INNER JOIN Finance.dbo.ChartOfAccount coa
        ON coa.Id = s.BudgetLineId
       AND coa.IsActive = 1
    WHERE dn.IsActive = 1
      AND dn.NoteDate BETWEEN @FromDate AND @ToDate

    UNION ALL

    ------------------------------------------------------------
    -- 6) MANUAL JOURNAL  (group by JournalNo)
    ------------------------------------------------------------
    SELECT
        mj.JournalDate        AS TransDate,
        mj.JournalNo          AS VoucherNo,
        'MJ'                  AS VoucherType,
        'Manual Journal'      AS VoucherName,
        coa.HeadName          AS AccountHeadName,
        mj.Description        AS Description,
        CAST(SUM(ISNULL(mj.Debit,  0.00)) AS DECIMAL(18,2)) AS Debit,
        CAST(SUM(ISNULL(mj.Credit, 0.00)) AS DECIMAL(18,2)) AS Credit
    FROM Finance.dbo.ManualJournal mj
    INNER JOIN Finance.dbo.ChartOfAccount coa
        ON coa.Id = mj.AccountId
       AND coa.IsActive = 1
    WHERE mj.IsActive = 1
      AND mj.JournalDate BETWEEN @FromDate AND @ToDate
    GROUP BY
        mj.JournalDate,
        mj.JournalNo,
        mj.Description,
        coa.HeadName
)

SELECT
    TransDate,
    VoucherNo,
    VoucherType,
    VoucherName,
    AccountHeadName,
    Description,
    Debit,
    Credit,
    RunningBalance =
        SUM(Debit - Credit) OVER (
            ORDER BY TransDate, VoucherType, VoucherNo
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        )
FROM DaybookRaw
ORDER BY TransDate, VoucherType, VoucherNo;
";

            return await Connection.QueryAsync<DaybookDTO>(
                sql,
                new
                {
                    dto.FromDate,
                    dto.ToDate,
                });
        }


    }


}