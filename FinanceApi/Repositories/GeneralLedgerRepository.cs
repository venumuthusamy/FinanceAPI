using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Repositories
{
    public class GeneralLedgerRepository : DynamicRepository,IGeneralLedgerRepository
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
-- 1) AR CONTROL (Invoices - Receipts - Credit Notes)
------------------------------------------------------------
ArInv AS (
    SELECT SUM(ISNULL(si.Subtotal, 0)) AS InvTotal
    FROM dbo.SalesInvoice si
    WHERE si.IsActive = 1
),
ArRec AS (
    SELECT SUM(ISNULL(r.AmountReceived, 0)) AS RecTotal
    FROM dbo.ArReceipt r
    WHERE r.IsActive = 1
),
ArCn AS (
    SELECT SUM(ISNULL(cn.Subtotal, 0)) AS CnTotal
    FROM dbo.CreditNote cn
    WHERE cn.IsActive = 1
),

------------------------------------------------------------
-- 2) AP CONTROL (PIN - Payments - Debit Notes)
------------------------------------------------------------
ApInv AS (
    SELECT SUM(ISNULL(si.Amount, 0) + ISNULL(si.Tax, 0)) AS InvTotal
    FROM dbo.SupplierInvoicePin si
    WHERE si.IsActive = 1
      AND si.Status   = 3
),
ApPay AS (
    SELECT SUM(ISNULL(sp.Amount, 0)) AS PayTotal
    FROM dbo.SupplierPayment sp
    WHERE sp.IsActive = 1
),
ApDn AS (
    SELECT SUM(ISNULL(dn.Amount, 0)) AS DnTotal
    FROM dbo.SupplierDebitNote dn
    WHERE dn.IsActive = 1
      AND dn.Status   = 2
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
    WHERE mj.IsActive   = 1
      AND mj.isPosted   = 1
      AND mj.IsRecurring = 0        -- ⭐ DO NOT take the template rows

    UNION ALL

    --------------------------------------------------------
    -- B) Sales Invoice lines
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
        ON coa.Id = i.BudgetLineId
       AND coa.IsActive = 1

    UNION ALL

    --------------------------------------------------------
    -- C) Credit Note lines
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

        OpeningBalance =
            CASE 
                WHEN coa.HeadCode = 101 THEN
                    ISNULL(arI.InvTotal, 0) - ISNULL(arC.CnTotal, 0)
                WHEN coa.HeadCode = 201 THEN
                    ISNULL(apI.InvTotal, 0) - ISNULL(apD.DnTotal, 0)
                ELSE
                    ISNULL(TRY_CONVERT(decimal(18,2), coa.OpeningBalance), 0)
            END,

        Movement =
            CASE 
                WHEN coa.HeadCode = 101 THEN
                    ISNULL(arR.RecTotal, 0)
                  + ISNULL(SUM(ISNULL(gl.Debit,0) - ISNULL(gl.Credit,0)), 0)
                WHEN coa.HeadCode = 201 THEN
                    ISNULL(apP.PayTotal, 0)
                  + ISNULL(SUM(ISNULL(gl.Debit,0) - ISNULL(gl.Credit,0)), 0)
                ELSE
                    ISNULL(SUM(ISNULL(gl.Debit,0) - ISNULL(gl.Credit,0)), 0)
            END
    FROM dbo.ChartOfAccount coa
    LEFT JOIN GlLines gl ON gl.HeadId = coa.Id

    LEFT JOIN ArInv arI ON coa.HeadCode = 101
    LEFT JOIN ArRec arR ON coa.HeadCode = 101
    LEFT JOIN ArCn  arC ON coa.HeadCode = 101

    LEFT JOIN ApInv apI ON coa.HeadCode = 201
    LEFT JOIN ApPay apP ON coa.HeadCode = 201
    LEFT JOIN ApDn  apD ON coa.HeadCode = 201

    WHERE coa.IsActive = 1
    GROUP BY
        coa.Id,
        coa.HeadCode,
        coa.HeadName,
        coa.OpeningBalance,
        coa.ParentHead,
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
        WHEN HeadCode IN (101,201)
            THEN OpeningBalance - Movement
        ELSE OpeningBalance + Movement
    END AS Balance
FROM Base
ORDER BY HeadCode;
";

            return await Connection.QueryAsync<GeneralLedgerDTO>(query);
        }


    }
}
