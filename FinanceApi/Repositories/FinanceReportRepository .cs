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
        public async Task<IEnumerable<TrialBalanceDTO>> GetTrialBalanceAsync(ReportBaseDTO dto)
        {
            const string sql = @"
;WITH
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
        coa.Id                                AS HeadId,   -- item inventory / expense head
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
    -- H) AP – Supplier Payment
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
    -- I) AP – Supplier Debit Note
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
        OpeningBalanceBase = ISNULL(TRY_CONVERT(decimal(18,2), coa.OpeningBalance),0)
    FROM dbo.ChartOfAccount coa
    LEFT JOIN GlLines gl
        ON gl.HeadId = coa.Id
    WHERE coa.IsActive = 1
    GROUP BY
        coa.Id,
        coa.HeadCode,
        coa.HeadName,
        coa.HeadType,
        coa.ParentHead,
        coa.OpeningBalance
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
        OpeningSigned_raw =
            CASE 
                WHEN HeadType IN ('A','E')   -- Assets / Expenses
                    THEN OpeningBalanceBase + OpeningMov
                ELSE                         -- Liabilities / Income / Equity
                    OpeningBalanceBase - OpeningMov
            END,
        ClosingSigned_raw =
            CASE 
                WHEN HeadType IN ('A','E')
                    THEN OpeningBalanceBase + OpeningMov + PeriodMov
                ELSE
                    OpeningBalanceBase - OpeningMov - PeriodMov
            END
    FROM Movements
),

------------------------------------------------------------
-- 4) COA HIERARCHY (each parent + all its children)
------------------------------------------------------------
Hierarchy AS (
    SELECT
        s.HeadId  AS ParentId,
        s.HeadId  AS ChildId
    FROM SignedRaw s

    UNION ALL

    SELECT
        h.ParentId,
        s.HeadId AS ChildId
    FROM Hierarchy h
    JOIN SignedRaw s
      ON s.ParentHead = h.ChildId
),

------------------------------------------------------------
-- 5) ROLLUP: PARENT HEAD = SUM(children balances)
------------------------------------------------------------
Aggregated AS (
    SELECT
        p.HeadId,
        p.HeadCode,
        p.HeadName,
        p.HeadType,
        p.ParentHead,
        OpeningSigned = SUM(ISNULL(c.OpeningSigned_raw,0)),
        ClosingSigned = SUM(ISNULL(c.ClosingSigned_raw,0))
    FROM SignedRaw p
    JOIN Hierarchy h
      ON h.ParentId = p.HeadId
    JOIN SignedRaw c
      ON c.HeadId = h.ChildId
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
-- if you want to hide completely empty accounts, keep this:
WHERE (OpeningSigned <> 0 OR ClosingSigned <> 0)
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
        //  TRIAL BALANCE DETAIL  (includes PINL for items)
        // ============================================================
        public async Task<IEnumerable<TrialBalanceDetailDTO>> GetTrialBalanceDetailAsync(
            TrialBalanceDetailRequestDTO dto)
        {
            const string sql = @"
;WITH GlLines AS (
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
    -- E) AR – Receipt
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
    -- H) AP – Supplier Payment
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
    }
}
