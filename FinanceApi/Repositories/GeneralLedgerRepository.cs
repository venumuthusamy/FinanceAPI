using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;

namespace FinanceApi.Repositories
{
    public class GeneralLedgerRepository : DynamicRepository, IGeneralLedgerRepository
    {
        public GeneralLedgerRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        public async Task<IEnumerable<GeneralLedgerDTO>> GetAllAsync()
        {
            const string query = @"
;WITH
------------------------------------------------------------
-- A) BUILD COA TREE TO GET ROOT HEAD TYPE
------------------------------------------------------------
CoaRoot AS (
    -- Anchor: top-level heads
    SELECT
        c.Id,
        c.HeadCode,
        c.HeadName,
        c.HeadType,
        c.ParentHead,
        RootHeadId   = c.Id,
        RootHeadType = c.HeadType
    FROM dbo.ChartOfAccount c
    WHERE c.IsActive = 1
      AND (c.ParentHead IS NULL OR c.ParentHead = 0)

    UNION ALL

    -- Recursive: children inherit root type from parent
    SELECT
        c.Id,
        c.HeadCode,
        c.HeadName,
        c.HeadType,
        c.ParentHead,
        r.RootHeadId,
        r.RootHeadType
    FROM dbo.ChartOfAccount c
    INNER JOIN CoaRoot r
        ON c.ParentHead = r.Id
    WHERE c.IsActive = 1
),

------------------------------------------------------------
-- 0) FIND AR / AP CONTROL HEADS (DYNAMIC BY NAME)
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
    -- AR INVOICES TOTAL = same as AR screen (from view)
    SELECT
        InvTotal = ISNULL(SUM(TRY_CONVERT(decimal(18,2), v.Amount)), 0)
    FROM dbo.vwSalesInvoiceOpenForReceipt v
),
ArRec AS (
    -- AR RECEIPTS TOTAL
    SELECT
        RecTotal = ISNULL(SUM(ISNULL(r.AmountReceived, 0)), 0)
    FROM dbo.ArReceipt r
),
ArCn AS (
    -- AR CREDIT NOTES TOTAL (header)
    SELECT
        CnTotal = ISNULL(SUM(TRY_CONVERT(decimal(18,2), cn.Subtotal)), 0)
    FROM dbo.CreditNote cn
    WHERE cn.IsActive = 1
),

------------------------------------------------------------
-- 2) AP CONTROL (PIN - Payments - Debit Notes)
------------------------------------------------------------
ApInv AS (
    SELECT
        InvTotal = ISNULL(SUM(ISNULL(si.Amount, 0) + ISNULL(si.Tax, 0)), 0)
    FROM dbo.SupplierInvoicePin si
    WHERE si.IsActive = 1
      AND si.Status   = 3          -- posted PIN
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
      AND dn.Status   = 2          -- posted DN
),

------------------------------------------------------------
-- 3) NORMAL GL LINES (MJ + SI + CN + PIN-from-JSON)
------------------------------------------------------------
GlLines AS (
    --------------------------------------------------------
    -- A) Manual Journal (ONLY non-recurring, posted rows)
    --------------------------------------------------------
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

    --------------------------------------------------------
    -- B) Sales Invoice lines  (item’s COA – asset gets credit)
    --------------------------------------------------------
    SELECT
        si.InvoiceDate                                    AS TransDate,
        'SI'                                              AS SourceType,
        sil.Id                                            AS SourceId,
        si.InvoiceNo                                      AS SourceNo,
        ISNULL(sil.Description, '')                       AS Description,
        coa.Id                                            AS HeadId,
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
        ON coa.Id = i.BudgetLineId   -- same COA as purchase (asset head)
       AND coa.IsActive = 1

    UNION ALL

    --------------------------------------------------------
    -- C) Credit Note lines  (P&L side)
    --------------------------------------------------------
    SELECT
        cn.CreditNoteDate                                 AS TransDate,
        'CN'                                              AS SourceType,
        cnl.Id                                            AS SourceId,
        cn.CreditNoteNo                                   AS SourceNo,
        'Credit Note for ' + ISNULL(cn.SiNumber,'')       AS Description,
        coa.Id                                            AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        ISNULL(TRY_CONVERT(decimal(18,2), cnl.LineNet ),0) +
        ISNULL(TRY_CONVERT(decimal(18,2), cnl.Tax     ),0) AS Debit,
        0                                                 AS Credit
    FROM dbo.CreditNoteLine cnl
    INNER JOIN dbo.CreditNote cn
        ON cn.Id = cnl.CreditNoteId
       AND cn.IsActive = 1
       AND cn.Status   = 3
    INNER JOIN dbo.Item i
        ON i.Id = cnl.ItemId
       AND i.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = i.BudgetLineId
       AND coa.IsActive = 1

    UNION ALL

    --------------------------------------------------------
    -- D) Supplier Invoice PIN lines from JSON (Asset/Expense)
    --------------------------------------------------------
    SELECT
        pin.InvoiceDate                                   AS TransDate,
        'PIN'                                             AS SourceType,
        pin.Id                                            AS SourceId,
        pin.InvoiceNo                                     AS SourceNo,
        'PIN line for item ' + j.item                     AS Description,
        coa.Id                                            AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        ISNULL(TRY_CONVERT(decimal(18,2), j.lineTotal),0) AS Debit,
        0                                                 AS Credit
    FROM dbo.SupplierInvoicePin pin
    CROSS APPLY OPENJSON(pin.LinesJson)
    WITH (
        item        nvarchar(50)  '$.item',
        location    nvarchar(100) '$.location',
        qty         decimal(18,4) '$.qty',
        unitPrice   decimal(18,4) '$.unitPrice',
        discountPct decimal(18,4) '$.discountPct',
        lineTotal   decimal(18,2) '$.lineTotal'
    ) AS j
    INNER JOIN dbo.Item i
        ON i.ItemCode = j.item
       AND i.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = i.BudgetLineId
       AND coa.IsActive = 1
    WHERE pin.IsActive = 1
      AND pin.Status   = 3
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
        cr.RootHeadType,                 -- root type (A / L / I / E / etc.)
        ISNULL(coa.ParentHead, 0) AS ParentHead,
        arH.ArHeadId,
        apH.ApHeadId,

        OpeningBalance =
            CASE 
                WHEN coa.Id = arH.ArHeadId THEN
                    ISNULL(arI.InvTotal, 0)
                WHEN coa.Id = apH.ApHeadId THEN
                    ISNULL(apI.InvTotal, 0)
                ELSE
                    ISNULL(TRY_CONVERT(decimal(18,2), coa.OpeningBalance), 0)
            END,

        DebitTotal =
            CASE
                WHEN coa.Id = arH.ArHeadId THEN
                    0
                WHEN coa.Id = apH.ApHeadId THEN
                    ISNULL(apP.PayTotal,0) + ISNULL(apD.DnTotal,0)
                ELSE
                    ISNULL(SUM(ISNULL(gl.Debit,0)),0)
            END,

        CreditTotal =
            CASE
                WHEN coa.Id = arH.ArHeadId THEN
                    ISNULL(arR.RecTotal,0) + ISNULL(arC.CnTotal,0)
                WHEN coa.Id = apH.ApHeadId THEN
                    0
                ELSE
                    ISNULL(SUM(ISNULL(gl.Credit,0)),0)
            END
    FROM dbo.ChartOfAccount coa
    INNER JOIN CoaRoot cr
        ON cr.Id = coa.Id
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
        cr.RootHeadType,
        coa.OpeningBalance,
        coa.ParentHead,
        arH.ArHeadId,
        apH.ApHeadId,
        arI.InvTotal,
        arR.RecTotal,
        arC.CnTotal,
        apI.InvTotal,
        apP.PayTotal,
        apD.DnTotal
)

------------------------------------------------------------
-- 5) FINAL RESULT
------------------------------------------------------------
SELECT
    HeadId,
    HeadCode,
    HeadName,
    HeadType,
    RootHeadType,      -- for debugging / UI filter
    ParentHead,
    OpeningBalance,
    DebitTotal  AS Debit,
    CreditTotal AS Credit,
    CASE 
        WHEN HeadId = ArHeadId THEN
            -- AR: Invoices - (Receipts + Credit Notes)
            OpeningBalance - CreditTotal

        WHEN HeadId = ApHeadId THEN
            -- AP: Invoices - (Payments + Debit Notes)
            OpeningBalance - DebitTotal

        WHEN RootHeadType = 'A' THEN
            -- ALL ASSET CHILDREN (Current Asset, Fixed Asset,
            -- Computer & IT Equipment, Accessories & Peripherals, etc.)
            -- show only Debit / Credit – NO balance.
            NULL           -- change to 0 if you prefer 0

        ELSE
            OpeningBalance + DebitTotal - CreditTotal
    END AS Balance
FROM Base
ORDER BY HeadCode
OPTION (MAXRECURSION 100);

";

            return await Connection.QueryAsync<GeneralLedgerDTO>(query);
        }
    }
}
