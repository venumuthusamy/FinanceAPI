using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using System.Collections.Generic;
using System.Threading.Tasks;

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
-- A) BUILD COA TREE TO GET ROOT HEAD TYPE (TEXT)
--    ParentHead stores PARENT HEADCODE, not Id
------------------------------------------------------------
CoaRoot AS (
    -- Root heads (no parent)
    SELECT
        c.Id,
        c.HeadCode,
        c.HeadName,
        c.HeadType,
        c.ParentHead,
        RootHeadId   = c.Id,
        RootHeadType =
            CASE c.HeadType
                WHEN 'A' THEN 'ASSET'
                WHEN 'L' THEN 'LIABILITY'
                WHEN 'E' THEN 'EQUITY'
                WHEN 'I' THEN 'INCOME'
                WHEN 'X' THEN 'EXPENSE'
                ELSE c.HeadType
            END
    FROM dbo.ChartOfAccount c
    WHERE c.IsActive = 1
      AND (c.ParentHead IS NULL OR c.ParentHead = 0)

    UNION ALL

    -- Children: ParentHead (HeadCode) → parent's HeadCode
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
        ON c.ParentHead = r.HeadCode
    WHERE c.IsActive = 1
),

------------------------------------------------------------
-- 0) FIND AR / AP CONTROL HEADS (ALWAYS ONE ROW)
------------------------------------------------------------
ArHead AS (
    SELECT
        ArHeadId = ISNULL((
            SELECT TOP (1) Id
            FROM dbo.ChartOfAccount
            WHERE IsActive = 1
              AND (
                    LTRIM(RTRIM(HeadName)) LIKE 'Account Receivable%'  -- singular
                 OR LTRIM(RTRIM(HeadName)) LIKE 'Accounts Receivable%' -- plural
              )
        ), 0)
),
ApHead AS (
    SELECT
        ApHeadId = ISNULL((
            SELECT TOP (1) Id
            FROM dbo.ChartOfAccount
            WHERE IsActive = 1
              AND (
                    LTRIM(RTRIM(HeadName)) LIKE 'Account Payable%'   -- singular
                 OR LTRIM(RTRIM(HeadName)) LIKE 'Accounts Payable%'  -- plural
              )
        ), 0)
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
-- 3) GL LINES (ACCOUNT LEVEL + ALL FLOWS)
------------------------------------------------------------
GlLines AS (
    --------------------------------------------------------
    -- A1) MANUAL JOURNAL (ONLY ACCOUNT-LEVEL, NO ITEM)
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
      AND mj.IsRecurring = 0
      AND (mj.ItemId IS NULL OR mj.ItemId = 0)   -- no item-linked MJ here

    UNION ALL
    --------------------------------------------------------
    -- A2) MANUAL JOURNAL – CUSTOMER SIDE (Customer.BudgetLineId)
    --------------------------------------------------------
    SELECT
        mj.JournalDate                                    AS TransDate,
        'MJ-CUST'                                         AS SourceType,
        mj.Id                                             AS SourceId,
        mj.JournalNo                                      AS SourceNo,
        'MJ for customer ' + ISNULL(c.CustomerName,'')    AS Description,
        c.BudgetLineId                                    AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        ISNULL(TRY_CONVERT(decimal(18,2), mj.Debit),  0)  AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), mj.Credit), 0)  AS Credit
    FROM dbo.ManualJournal mj
    INNER JOIN dbo.Customer c
        ON c.Id = mj.CustomerId
       AND c.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = c.BudgetLineId
       AND coa.IsActive = 1
    WHERE mj.IsActive    = 1
      AND mj.IsRecurring = 0
      AND mj.CustomerId IS NOT NULL

    UNION ALL
    --------------------------------------------------------
    -- A3) MANUAL JOURNAL – SUPPLIER SIDE (Supplier.BudgetLineId)
    --------------------------------------------------------
    SELECT
        mj.JournalDate                                    AS TransDate,
        'MJ-SUPP'                                         AS SourceType,
        mj.Id                                             AS SourceId,
        mj.JournalNo                                      AS SourceNo,
        'MJ for supplier ' + ISNULL(s.Name,'')            AS Description,
        s.BudgetLineId                                    AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        ISNULL(TRY_CONVERT(decimal(18,2), mj.Debit),  0)  AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), mj.Credit), 0)  AS Credit
    FROM dbo.ManualJournal mj
    INNER JOIN dbo.Suppliers s
        ON s.Id = mj.SupplierId
       AND s.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = s.BudgetLineId
       AND coa.IsActive = 1
    WHERE mj.IsActive    = 1
      AND mj.IsRecurring = 0
      AND mj.SupplierId IS NOT NULL

    UNION ALL
    --------------------------------------------------------
    -- B) Sales Invoice – CUSTOMER AR SIDE (Control account)
    --------------------------------------------------------
    SELECT
        si.InvoiceDate                                    AS TransDate,
        'SI-AR'                                           AS SourceType,
        si.Id                                             AS SourceId,
        si.InvoiceNo                                      AS SourceNo,
        'Invoice to ' + ISNULL(c.CustomerName,'')         AS Description,
        c.BudgetLineId                                    AS HeadId,
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
    -- C) AR Receipt Allocation – CUSTOMER AR SIDE
    --------------------------------------------------------
    SELECT
        r.ReceiptDate                                     AS TransDate,
        'AR-REC'                                          AS SourceType,
        r.Id                                              AS SourceId,
        r.ReceiptNo                                       AS SourceNo,
        'Receipt from ' + ISNULL(c.CustomerName,'')       AS Description,
        c.BudgetLineId                                    AS HeadId,
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
    -- D) Credit Note – CUSTOMER AR SIDE
    --------------------------------------------------------
    SELECT
        cn.CreditNoteDate                                 AS TransDate,
        'CN-AR'                                           AS SourceType,
        cn.Id                                             AS SourceId,
        cn.CreditNoteNo                                   AS SourceNo,
        'Credit Note to ' + ISNULL(
            COALESCE(c1.CustomerName, c2.CustomerName),'' ) AS Description,
        COALESCE(c1.BudgetLineId, c2.BudgetLineId)        AS HeadId,
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

    --------------------------------------------------------
    -- E) Sales Invoice Line – REVENUE side  (SalesInvoiceLine.BudgetLineId)
    --------------------------------------------------------
    UNION ALL
    SELECT
        si.InvoiceDate                                    AS TransDate,
        'SI-LN'                                           AS SourceType,
        si.Id                                             AS SourceId,
        si.InvoiceNo                                      AS SourceNo,
        'Revenue for ' + 
            ISNULL(i.ItemName, sil.ItemName)              AS Description,
        sil.BudgetLineId                                  AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        0                                                 AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), sil.LineAmount),0) AS Credit
    FROM dbo.SalesInvoiceLine sil
    INNER JOIN dbo.SalesInvoice si
        ON si.Id = sil.SiId
       AND si.IsActive = 1
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = sil.BudgetLineId
       AND coa.IsActive = 1
    LEFT JOIN Finance.dbo.Item i
        ON i.Id = sil.ItemId
       AND i.IsActive = 1

    --------------------------------------------------------
    -- F) Supplier PIN Line – EXPENSE / COST side (LinesJson.budgetLineId)
    --------------------------------------------------------
    UNION ALL
    SELECT
        pin.InvoiceDate                                   AS TransDate,
        'PIN-LN'                                          AS SourceType,
        pin.Id                                            AS SourceId,
        pin.InvoiceNo                                     AS SourceNo,
        'PIN line for ' + ISNULL(j.item,'')               AS Description,
        j.budgetLineId                                    AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        ISNULL(TRY_CONVERT(decimal(18,2), j.lineTotal),0) AS Debit,
        0                                                 AS Credit
    FROM dbo.SupplierInvoicePin pin
    CROSS APPLY OPENJSON(pin.LinesJson)
    WITH (
        item         nvarchar(50)  '$.item',
        location     nvarchar(100) '$.location',
        budgetLineId int           '$.budgetLineId',
        qty          decimal(18,4) '$.qty',
        unitPrice    decimal(18,4) '$.unitPrice',
        discountPct  decimal(18,4) '$.discountPct',
        lineTotal    decimal(18,2) '$.lineTotal'
    ) AS j
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = j.budgetLineId
       AND coa.IsActive = 1
    WHERE pin.IsActive = 1
      AND pin.Status   = 3

    --------------------------------------------------------
    -- G) Supplier Invoice PIN – AP control side (Supplier.BudgetLineId)
    --------------------------------------------------------
    UNION ALL
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
    -- H) Supplier Debit Note – AP side
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
    -- I) Supplier Payment – AP side
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
-- 3B) ITEM-LEVEL LINES  (for Item children)
------------------------------------------------------------
ItemLines AS (
    --------------------------------------------------------
    -- 1) Sales Invoice item lines (Credit)
    --------------------------------------------------------
    SELECT
        i.Id AS ItemId,
        Debit  = 0,
        Credit = ISNULL(TRY_CONVERT(decimal(18,2), sil.LineAmount),0) +
                 ISNULL(TRY_CONVERT(decimal(18,2), sil.Tax),0)
    FROM dbo.SalesInvoiceLine sil
    INNER JOIN dbo.SalesInvoice si
        ON si.Id = sil.SiId
       AND si.IsActive = 1
    INNER JOIN Finance.dbo.Item i
        ON i.Id = sil.ItemId
       AND i.IsActive = 1

    UNION ALL
    --------------------------------------------------------
    -- 2) Credit Note item lines (Debit)
    --------------------------------------------------------
    SELECT
        i.Id AS ItemId,
        Debit  = ISNULL(TRY_CONVERT(decimal(18,2), cnl.LineNet ),0) +
                 ISNULL(TRY_CONVERT(decimal(18,2), cnl.Tax     ),0),
        Credit = 0
    FROM dbo.CreditNoteLine cnl
    INNER JOIN dbo.CreditNote cn
        ON cn.Id = cnl.CreditNoteId
       AND cn.IsActive  = 1
       AND cnl.IsActive = 1
    INNER JOIN Finance.dbo.Item i
        ON i.Id = cnl.ItemId
       AND i.IsActive = 1

    UNION ALL
    --------------------------------------------------------
    -- 3) Supplier PIN item JSON lines (Debit) – Item-based view
    --------------------------------------------------------
    SELECT
        i.Id AS ItemId,
        Debit  = ISNULL(TRY_CONVERT(decimal(18,2), j.lineTotal),0),
        Credit = 0
    FROM dbo.SupplierInvoicePin pin
    CROSS APPLY OPENJSON(pin.LinesJson)
    WITH (
        item         nvarchar(50)  '$.item',
        location     nvarchar(100) '$.location',
        budgetLineId int           '$.budgetLineId',
        qty          decimal(18,4) '$.qty',
        unitPrice    decimal(18,4) '$.unitPrice',
        discountPct  decimal(18,4) '$.discountPct',
        lineTotal    decimal(18,2) '$.lineTotal'
    ) AS j
    INNER JOIN Finance.dbo.Item i
        ON i.ItemCode = j.item
       AND i.IsActive = 1
    WHERE pin.IsActive = 1
      AND pin.Status   = 3

    UNION ALL
    --------------------------------------------------------
    -- 4) Manual Journal linked to Item (Debit/Credit)
    --------------------------------------------------------
    SELECT
        i.Id AS ItemId,
        Debit  = ISNULL(TRY_CONVERT(decimal(18,2), mj.Debit), 0),
        Credit = ISNULL(TRY_CONVERT(decimal(18,2), mj.Credit), 0)
    FROM dbo.ManualJournal mj
    INNER JOIN Finance.dbo.Item i
        ON i.Id = mj.ItemId
       AND i.IsActive = 1
    WHERE mj.IsActive    = 1
      AND mj.IsRecurring = 0
),

------------------------------------------------------------
-- 4) OPENING BALANCE FROM OpeningBalance TABLE
--    (JOIN via BudgetLineId -> ChartOfAccount.Id)
------------------------------------------------------------
Opening AS (
    SELECT
        coa.Id AS HeadId,
        OpeningBalance = SUM(ISNULL(ob.OpeningBalanceAmount, 0))
    FROM dbo.OpeningBalance ob
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id      = ob.BudgetLineId   -- <<=== IMPORTANT JOIN
       AND coa.IsActive = 1
    WHERE ob.IsActive = 1
    GROUP BY coa.Id
),

------------------------------------------------------------
-- 5) BASE SUMMARY PER HEAD (UNIFORM LOGIC)
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

        OpeningBalance = ISNULL(ob.OpeningBalance, 0),

        DebitTotal  = ISNULL(SUM(ISNULL(gl.Debit,  0)), 0),
        CreditTotal = ISNULL(SUM(ISNULL(gl.Credit, 0)), 0)
    FROM dbo.ChartOfAccount coa
    INNER JOIN CoaRoot cr
        ON cr.Id = coa.Id
    LEFT JOIN GlLines gl
        ON gl.HeadId = coa.Id
    LEFT JOIN Opening ob
        ON ob.HeadId = coa.Id
    CROSS JOIN ArHead arH
    CROSS JOIN ApHead apH
    WHERE coa.IsActive = 1
    GROUP BY
        coa.Id,
        coa.HeadCode,
        coa.HeadName,
        coa.HeadType,
        cr.RootHeadType,
        coa.ParentHead,
        ob.OpeningBalance,
        arH.ArHeadId,
        apH.ApHeadId
),

------------------------------------------------------------
-- 6) ITEM NODES (CHILDREN UNDER BUDGET LINE)
------------------------------------------------------------
ItemBase AS (
    SELECT
        HeadId       = 1000000 + i.Id,                    -- unique Id for item-node
        HeadCode     = 1000000 + i.Id,                    -- unique numeric code
        HeadName     = i.ItemCode + ' - ' + i.ItemName,   -- e.g. ITM001 - Laptop
        HeadType     = coa.HeadType,
        RootHeadType = cr.RootHeadType,
        ParentHead   = coa.HeadCode,                      -- parent = budget line HEADCODE
        OpeningBalance = 0,
        DebitTotal     = ISNULL(SUM(ISNULL(il.Debit, 0)), 0),
        CreditTotal    = ISNULL(SUM(ISNULL(il.Credit,0)), 0)
    FROM Finance.dbo.Item i
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = i.BudgetLineId
       AND coa.IsActive = 1
    INNER JOIN CoaRoot cr
        ON cr.Id = coa.Id
    LEFT JOIN ItemLines il
        ON il.ItemId = i.Id
    WHERE i.IsActive = 1
    GROUP BY
        i.Id,
        i.ItemCode,
        i.ItemName,
        coa.HeadType,
        cr.RootHeadType,
        coa.HeadCode
),

------------------------------------------------------------
-- 7) ACCOUNTS WHICH HAVE ITEM CHILDREN
------------------------------------------------------------
AssetWithItems AS (
    SELECT DISTINCT
        coa.Id AS HeadId
    FROM Finance.dbo.Item i
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = i.BudgetLineId
       AND coa.IsActive = 1
    WHERE i.IsActive = 1
)

------------------------------------------------------------
-- 8) FINAL RESULT  (ACCOUNTS + ITEM CHILDREN)
------------------------------------------------------------
SELECT
    b.HeadId,
    b.HeadCode,
    b.HeadName,
    b.HeadType,
    b.RootHeadType,
    b.ParentHead,

    -- ✅ OpeningBalance epovume table-la irundhu dhan varanum
    OpeningBalance = b.OpeningBalance,

    -- ✅ Debit / Credit mattum items irundha 0 pannrom
    Debit =
        CASE WHEN awi.HeadId IS NOT NULL THEN 0
             ELSE b.DebitTotal
        END,

    Credit =
        CASE WHEN awi.HeadId IS NOT NULL THEN 0
             ELSE b.CreditTotal
        END,

    0 AS Balance,   -- UI: Opening + Credit − Debit

    CASE
        WHEN b.HeadId = b.ArHeadId OR b.HeadId = b.ApHeadId
        THEN 1 ELSE 0
    END AS IsControl,

    1 AS IsActive
FROM Base b
LEFT JOIN AssetWithItems awi
       ON awi.HeadId = b.HeadId

UNION ALL

-- Item rows (child of the BudgetLine head)
SELECT
    HeadId,
    HeadCode,
    HeadName,
    HeadType,
    RootHeadType,
    ParentHead,
    OpeningBalance,
    DebitTotal  AS Debit,
    CreditTotal AS Credit,
    0 AS Balance,
    0 AS IsControl,
    1 AS IsActive
FROM ItemBase

ORDER BY HeadCode
OPTION (MAXRECURSION 0);


";

            return await Connection.QueryAsync<GeneralLedgerDTO>(query);
        }





    }
}
