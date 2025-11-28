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
-- Open invoices (same as AR screen)
ArInv AS (
    SELECT
        InvTotal = ISNULL(SUM(TRY_CONVERT(decimal(18,2), v.Amount)), 0)
    FROM dbo.vwSalesInvoiceOpenForReceipt v
),

-- Receipts: allocated amount
ArRec AS (
    SELECT
        RecTotal = ISNULL(SUM(ISNULL(ra.AllocatedAmount, 0)), 0)
    FROM dbo.ArReceiptAllocation ra
    WHERE ra.IsActive = 1
),

-- Credit notes: customer AR side (LineNet + Tax)
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
-- 3) GL LINES
------------------------------------------------------------
GlLines AS (

    --------------------------------------------------------
    -- A) Manual Journal
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
    -- B) Sales Invoice – CUSTOMER AR SIDE (Dr Customer)
    --------------------------------------------------------
    SELECT
        si.InvoiceDate                                    AS TransDate,
        'SI-AR'                                           AS SourceType,
        si.Id                                             AS SourceId,
        si.InvoiceNo                                      AS SourceNo,
        'Invoice to ' + ISNULL(c.CustomerName,'')         AS Description,
        c.BudgetLineId                                    AS HeadId,  -- CUSTOMER COA (e.g. Gowtham)
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

    --------------------------------------------------------
    -- C) AR Receipt Allocation – CUSTOMER AR SIDE (Cr Customer)
    --    Customer from ArReceipt.CustomerId
    --------------------------------------------------------
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

    --------------------------------------------------------
    -- D) Credit Note – CUSTOMER AR SIDE (Cr Customer)
    --    Same data as ArCn (LineNet + Tax)
    --------------------------------------------------------
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
    LEFT JOIN dbo.Customer c1              -- direct customer on CN
        ON c1.Id = cn.CustomerId
       AND c1.IsActive = 1
    LEFT JOIN dbo.SalesInvoice si_cn       -- fallback via SI -> SO -> Customer
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

    --------------------------------------------------------
    -- E) Sales Invoice lines  (item P&L side)
    --------------------------------------------------------
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

    --------------------------------------------------------
    -- F) Credit Note lines  (item P&L side)
    --------------------------------------------------------
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

    --------------------------------------------------------
    -- G) Supplier Invoice PIN lines (asset/expense side)
    --------------------------------------------------------
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

    UNION ALL

    --------------------------------------------------------
    -- H) Supplier Invoice PIN – AP side (Supplier head)
    --------------------------------------------------------
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

    --------------------------------------------------------
    -- I) Supplier Debit Note – AP side (Supplier head)
    --------------------------------------------------------
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

    --------------------------------------------------------
    -- J) Supplier Payment – AP side (Supplier head)
    --------------------------------------------------------
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
        cr.RootHeadType,
        ISNULL(coa.ParentHead, 0) AS ParentHead,
        arH.ArHeadId,
        apH.ApHeadId,

        -- 🔹 OpeningBalance:
        --    AR head  -> ArInv total
        --    AP head  -> ApInv total
        --    Others   -> 0 (since COA opening balance column removed)
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
    RootHeadType,
    ParentHead,

    -- OpeningBalance only for AR / AP control
    CASE 
        WHEN HeadId = ArHeadId OR HeadId = ApHeadId
            THEN OpeningBalance
        ELSE NULL
    END AS OpeningBalance,

    DebitTotal  AS Debit,
    CreditTotal AS Credit,

    CASE 
        WHEN HeadId = ArHeadId THEN
            OpeningBalance - CreditTotal          -- AR = Inv - (Rec + CN)
        WHEN HeadId = ApHeadId THEN
            OpeningBalance - DebitTotal           -- AP
        WHEN RootHeadType = 'A' THEN
            NULL                                  -- child assets – hide balance
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
