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
-- 0) FIND AR / AP CONTROL HEADS (DYNAMIC BY NAME)
------------------------------------------------------------
ArHead AS (
    SELECT TOP (1) Id AS ArHeadId
    FROM dbo.ChartOfAccount
    WHERE IsActive = 1
      AND HeadName = 'Account Receivable'
),
ApHead AS (
    SELECT TOP (1) Id AS ApHeadId
    FROM dbo.ChartOfAccount
    WHERE IsActive = 1
      AND HeadName = 'Account Payable'
),

------------------------------------------------------------
-- 1) AR CONTROL (Invoices - Receipts - Credit Notes)
------------------------------------------------------------
ArInv AS (
    -- AR = same as P&L line expression (LineAmount + Tax)
    SELECT
        InvTotal = SUM(
            ISNULL(TRY_CONVERT(decimal(18,2), sil.LineAmount),0) +
            ISNULL(TRY_CONVERT(decimal(18,2), sil.Tax),0)
        )
    FROM dbo.SalesInvoice si
    INNER JOIN dbo.SalesInvoiceLine sil
        ON sil.SiId = si.Id
    WHERE si.IsActive = 1
      AND si.Status   = 3        -- posted only (adjust if needed)
),
ArRec AS (
    SELECT SUM(ISNULL(r.AmountReceived, 0)) AS RecTotal
    FROM dbo.ArReceipt r
    WHERE r.IsActive = 1          -- add Status filter here if you have it
),
ArCn AS (
    -- AR credit notes = same pattern as CN P&L lines
    SELECT
        CnTotal = SUM(
            ISNULL(TRY_CONVERT(decimal(18,2), cnl.LineNet),0) +
            ISNULL(TRY_CONVERT(decimal(18,2), cnl.Tax),0)
        )
    FROM dbo.CreditNote cn
    INNER JOIN dbo.CreditNoteLine cnl
        ON cnl.CreditNoteId = cn.Id
    WHERE cn.IsActive = 1
      AND cn.Status   = 3        -- posted CN only (adjust if needed)
),

------------------------------------------------------------
-- 2) AP CONTROL (PIN - Payments - Debit Notes)
------------------------------------------------------------
ApInv AS (
    SELECT SUM(ISNULL(si.Amount, 0) + ISNULL(si.Tax, 0)) AS InvTotal
    FROM dbo.SupplierInvoicePin si
    WHERE si.IsActive = 1
      AND si.Status   = 3        -- posted PIN
),
ApPay AS (
    SELECT SUM(ISNULL(sp.Amount, 0)) AS PayTotal
    FROM dbo.SupplierPayment sp
    WHERE sp.IsActive = 1         -- add Status filter if you have
),
ApDn AS (
    SELECT 
        SUM(
            ISNULL(
                CASE 
                    WHEN dn.Amount < 0 THEN -dn.Amount   -- make DN positive
                    ELSE dn.Amount
                END, 0)
        ) AS DnTotal
    FROM dbo.SupplierDebitNote dn
    WHERE dn.IsActive = 1
      AND dn.Status   = 2         -- posted DN (your current convention)
),

------------------------------------------------------------
-- 3) NORMAL GL LINES (MJ + SI lines + CN lines)
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
      AND mj.IsRecurring = 0        -- do not take template rows

    UNION ALL

    --------------------------------------------------------
    -- B) Sales Invoice lines  (P&L side)
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
       AND si.Status   = 3          -- posted invoice
    INNER JOIN dbo.Item i
        ON i.Id = sil.ItemId
       AND i.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = i.BudgetLineId   -- item -> budget line
       AND coa.IsActive = 1

    UNION ALL

    --------------------------------------------------------
    -- C) Credit Note lines (P&L side)
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
       AND cn.Status   = 3          -- posted CN
    INNER JOIN dbo.Item i
        ON i.Id = cnl.ItemId
       AND i.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = i.BudgetLineId
       AND coa.IsActive = 1
),

------------------------------------------------------------
-- 4) BASE SUMMARY PER HEAD (Opening + Movement)
------------------------------------------------------------
Base AS (
    SELECT
        coa.Id        AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        ISNULL(coa.ParentHead, 0) AS ParentHead,
        arH.ArHeadId,
        apH.ApHeadId,

        ----------------------------------------------------
        -- OpeningBalance
        -- AR control = AR invoices total
        -- AP control = AP PIN invoices total
        ----------------------------------------------------
        OpeningBalance =
            CASE 
                WHEN coa.Id = arH.ArHeadId THEN
                    ISNULL(arI.InvTotal, 0)
                WHEN coa.Id = apH.ApHeadId THEN
                    ISNULL(apI.InvTotal, 0)
                ELSE
                    ISNULL(TRY_CONVERT(decimal(18,2), coa.OpeningBalance), 0)
            END,

        ----------------------------------------------------
        -- Movement (Received column)
        -- AR: Receipts + Credit Notes + GL movements
        -- AP: Payments + Debit Notes + GL movements
        ----------------------------------------------------
        Movement =
            CASE 
                WHEN coa.Id = arH.ArHeadId THEN
                    ISNULL(arR.RecTotal, 0)
                  + ISNULL(arC.CnTotal, 0)
                  + ISNULL(SUM(ISNULL(gl.Debit,0) - ISNULL(gl.Credit,0)), 0)
                WHEN coa.Id = apH.ApHeadId THEN
                    ISNULL(apP.PayTotal, 0)
                  + ISNULL(apD.DnTotal, 0)
                  + ISNULL(SUM(ISNULL(gl.Debit,0) - ISNULL(gl.Credit,0)), 0)
                ELSE
                    ISNULL(SUM(ISNULL(gl.Debit,0) - ISNULL(gl.Credit,0)), 0)
            END
    FROM dbo.ChartOfAccount coa
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

SELECT
    HeadId,
    HeadCode,
    HeadName,
    ParentHead,
    OpeningBalance,
    Movement AS Received,
    CASE 
        WHEN HeadId IN (ArHeadId, ApHeadId)
            THEN OpeningBalance - Movement   -- AR / AP control
        ELSE OpeningBalance + Movement       -- normal GL
    END AS Balance
FROM Base
ORDER BY HeadCode;
";

            return await Connection.QueryAsync<GeneralLedgerDTO>(query);
        }

    }
}
