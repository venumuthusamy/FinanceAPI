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
-- A) SAFE COA TREE (ParentHead = parent HEADCODE)
------------------------------------------------------------
CoaCTE AS (
    -- ROOT (no parent)
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
            END,
        Level = 0
    FROM dbo.ChartOfAccount c
    WHERE (c.ParentHead IS NULL OR c.ParentHead = 0)
      AND c.IsActive = 1

    UNION ALL

    -- CHILDREN
    SELECT
        c.Id,
        c.HeadCode,
        c.HeadName,
        c.HeadType,
        c.ParentHead,
        cr.RootHeadId,
        cr.RootHeadType,
        Level = cr.Level + 1
    FROM dbo.ChartOfAccount c
    INNER JOIN CoaCTE cr
        ON c.ParentHead = cr.HeadCode    -- ParentHead stores parent HEADCODE
    WHERE c.IsActive = 1
      AND cr.Level < 20
),
CoaRoot AS (
    SELECT
        Id,
        HeadCode,
        HeadName,
        HeadType,
        ParentHead,
        RootHeadId,
        RootHeadType
    FROM (
        SELECT
            cte.*,
            ROW_NUMBER() OVER (PARTITION BY cte.Id ORDER BY cte.Level) AS rn
        FROM CoaCTE cte
    ) x
    WHERE x.rn = 1
),

------------------------------------------------------------
-- B) GL LINES (all modules)
------------------------------------------------------------
GlLines AS (

    --------------------------------------------------------
    -- 1 ► Opening Balance
    --------------------------------------------------------
    SELECT
        ob.BudgetLineId AS HeadId,
        OpeningBalance = ISNULL(ob.OpeningBalanceAmount, 0),
        Debit  = 0,
        Credit = 0
    FROM dbo.OpeningBalance ob
WHERE ob.IsActive = 1;
    UNION ALL

    --------------------------------------------------------
    -- 2 ► Manual Journal (original side – BudgetLineId)
    --------------------------------------------------------
    SELECT
        mj.BudgetLineId AS HeadId,
        OpeningBalance = 0,
        Debit  = ISNULL(mj.Debit,  0),
        Credit = ISNULL(mj.Credit, 0)
    FROM dbo.ManualJournal mj
    WHERE mj.IsActive = 1
      AND mj.isPosted = 1

    UNION ALL

    --------------------------------------------------------
    -- 2b ► Manual Journal – CASH side for Type = 'SUPPLIER'
    --      Supplier type: DR Expense/Supplier, CR Cash
    --------------------------------------------------------
    SELECT
        cashCoa.Id AS HeadId,
        OpeningBalance = 0,
        Debit  = 0,
        Credit = ISNULL(mj.Debit, 0)
    FROM dbo.ManualJournal mj
    CROSS JOIN dbo.ChartOfAccount cashCoa
    WHERE mj.IsActive = 1
      AND mj.isPosted = 1
      AND UPPER(mj.[Type]) = 'SUPPLIER'      -- adjust value if needed
      AND ISNULL(mj.Debit, 0) <> 0
      AND cashCoa.IsActive = 1
      AND (
            cashCoa.HeadName = 'Cash'
      )

    UNION ALL

    --------------------------------------------------------
    -- 2c ► Manual Journal – CASH side for Type = 'CUSTOMER'
    --      Customer type: DR Cash, CR Income/Customer
    --------------------------------------------------------
    SELECT
        cashCoa.Id AS HeadId,
        OpeningBalance = 0,
        Debit  = ISNULL(mj.Credit, 0),
        Credit = 0
    FROM dbo.ManualJournal mj
    CROSS JOIN dbo.ChartOfAccount cashCoa
    WHERE mj.IsActive = 1
      AND mj.isPosted = 1
      AND UPPER(mj.[Type]) = 'CUSTOMER'      -- adjust value if needed
      AND ISNULL(mj.Credit, 0) <> 0
      AND cashCoa.IsActive = 1
      AND (
            cashCoa.HeadName = 'Cash'

      )

    UNION ALL

    --------------------------------------------------------
    -- 3 ► Sales Invoice – DR Customer, CR Income/Asset
    --------------------------------------------------------
    -- DR Customer  (AR +)
    SELECT
        coaCust.Id AS HeadId,                -- COA Id from Customer.BudgetLineId
        0,
        Debit  = ISNULL(sil.LineAmount, 0),
        0
    FROM dbo.SalesInvoiceLine sil
    INNER JOIN dbo.SalesInvoice si ON si.Id = sil.SiId
    INNER JOIN dbo.SalesOrder  so ON so.Id = si.SoId
    INNER JOIN dbo.Customer    cs ON cs.Id = so.CustomerId
    INNER JOIN dbo.ChartOfAccount coaCust
        ON coaCust.Id = cs.BudgetLineId
        -- IF Customer.BudgetLineId stores HEADCODE instead of Id:
        -- ON coaCust.HeadCode = cs.BudgetLineId

    UNION ALL

    -- CR Income / Asset (item line’s BudgetLineId already COA Id)
    SELECT
        sil.BudgetLineId AS HeadId,
        0,
        0,
        Credit = ISNULL(sil.LineAmount, 0)
    FROM dbo.SalesInvoiceLine sil

    UNION ALL

    --------------------------------------------------------
    -- 4 ► Supplier Invoice PIN – DR Expense/Asset, CR Supplier
    --------------------------------------------------------
    -- CR Supplier (AP +)
    SELECT
        sps.BudgetLineId AS HeadId,
        0,
        0,
        Credit = ISNULL(sip.Amount, 0)
    FROM dbo.SupplierInvoicePin sip
    INNER JOIN dbo.Suppliers sps ON sps.Id = sip.SupplierId
    WHERE sip.IsActive = 1

    UNION ALL

    -- DR Expense / Asset from PIN lines  (JSON: budgetLineId + lineTotal)
    SELECT
        L.BudgetLineId AS HeadId,
        0,
        Debit = ISNULL(L.LineTotal, 0),
        0
    FROM dbo.SupplierInvoicePin sip
    CROSS APPLY OPENJSON(sip.LinesJson)
    WITH (
        BudgetLineId INT            '$.budgetLineId',
        LineTotal    DECIMAL(18,2)  '$.lineTotal'
    ) AS L
    WHERE sip.IsActive = 1

    UNION ALL

    --------------------------------------------------------
    -- 5 ► AR Receipt – DR Bank / Cash, CR Customer
    --------------------------------------------------------

    -- 5a) DR BANK  (BankId NOT NULL)
    SELECT
        bk.BudgetLineId AS HeadId,           -- BANK → COA
        0,
        Debit  = ISNULL(ar.TotalAllocated, 0),
        0
    FROM dbo.ArReceipt ar
    INNER JOIN dbo.Bank bk ON bk.Id = ar.BankId
    WHERE ar.IsActive = 1
      AND ar.BankId IS NOT NULL

    UNION ALL

    -- 5b) DR CASH-IN-HAND  (BankId NULL or 0)
    SELECT
        cashCoa.Id AS HeadId,                -- Cash-in-hand COA Id
        0,
        Debit = ISNULL(ar.TotalAllocated, 0),
        0
    FROM dbo.ArReceipt ar
    CROSS JOIN dbo.ChartOfAccount cashCoa
    WHERE ar.IsActive = 1
      AND (ar.BankId IS NULL OR ar.BankId = 0)
      AND cashCoa.IsActive = 1
      AND (
            cashCoa.HeadName = 'Cash-in-hand'
         OR cashCoa.HeadCode = 10102       -- put your Cash-in-hand HeadCode here
      )

    UNION ALL

    -- 5c) CR Customer (AR −)
    SELECT
        coaCust.Id AS HeadId,
        0,
        0,
        Credit = ISNULL(ra.AllocatedAmount, 0)
    FROM dbo.ArReceiptAllocation ra
    INNER JOIN dbo.ArReceipt    ar ON ar.Id = ra.ReceiptId
    INNER JOIN dbo.SalesInvoice si ON si.Id = ra.InvoiceId
    INNER JOIN dbo.SalesOrder  so ON so.Id = si.SoId
    INNER JOIN dbo.Customer    cs ON cs.Id = so.CustomerId
    INNER JOIN dbo.ChartOfAccount coaCust
        ON coaCust.Id = cs.BudgetLineId
        -- or: ON coaCust.HeadCode = cs.BudgetLineId
    WHERE ra.IsActive = 1

    UNION ALL

    --------------------------------------------------------
    -- 6 ► Supplier Payment – DR Supplier, CR Bank
    --------------------------------------------------------
    -- DR Supplier (AP −)
    SELECT
        sps.BudgetLineId AS HeadId,
        0,
        Debit = ISNULL(sp.Amount, 0),
        0
    FROM dbo.SupplierPayment sp
    INNER JOIN dbo.Suppliers sps ON sps.Id = sp.SupplierId
    WHERE sp.IsActive = 1

    UNION ALL

    -- CR Bank (Bank −)
    SELECT
        bk.BudgetLineId AS HeadId,
        0,
        0,
        Credit = ISNULL(sp.Amount, 0)
    FROM dbo.SupplierPayment sp
    INNER JOIN dbo.Bank bk ON bk.Id = sp.BankId
    WHERE sp.IsActive = 1

    UNION ALL

    --------------------------------------------------------
    -- 7 ► Supplier Debit Note – DR Supplier, CR Expense/Income
    --------------------------------------------------------
    -- DR Supplier (AP +)
    SELECT
        sps.BudgetLineId AS HeadId,
        0,
        Debit = ISNULL(sdn.Amount, 0),
        0
    FROM dbo.SupplierDebitNote sdn
    INNER JOIN dbo.Suppliers sps ON sps.Id = sdn.SupplierId
    WHERE sdn.IsActive = 1

    UNION ALL

    -- CR Expense / Income (lines)
    SELECT
        L.BudgetLineId AS HeadId,
        0,
        0,
        Credit = ISNULL(L.Amount, 0)
    FROM dbo.SupplierDebitNote sdn
    CROSS APPLY OPENJSON(sdn.LinesJson)
    WITH (
        BudgetLineId INT            '$.budgetLineId',
        Amount       DECIMAL(18,2)  '$.amount'
    ) AS L
    WHERE sdn.IsActive = 1

    UNION ALL

    --------------------------------------------------------
    -- 8 ► Customer Credit Note – DR Income, CR Customer
    --------------------------------------------------------
    -- DR Income (item’s account)
    SELECT
        siLine.BudgetLineId AS HeadId,
        0,
        Debit = ISNULL(cn.Subtotal, 0),
        0
    FROM dbo.CreditNote cn
    INNER JOIN dbo.SalesInvoice     si     ON si.Id = cn.SiId
    INNER JOIN dbo.SalesInvoiceLine siLine ON siLine.SiId = si.Id
    WHERE cn.IsActive = 1

    UNION ALL

    -- CR Customer (AR −)
    SELECT
        coaCust.Id AS HeadId,
        0,
        0,
        Credit = ISNULL(cn.Subtotal, 0)
    FROM dbo.CreditNote cn
    INNER JOIN dbo.Customer cs ON cs.Id = cn.CustomerId
    INNER JOIN dbo.ChartOfAccount coaCust
        ON coaCust.Id = cs.BudgetLineId
        -- or: ON coaCust.HeadCode = cs.BudgetLineId
    WHERE cn.IsActive = 1
),

------------------------------------------------------------
-- C) Aggregate per Head
------------------------------------------------------------
GlAgg AS (
    SELECT
        HeadId,
        OpeningBalance = SUM(ISNULL(OpeningBalance, 0)),
        Debit          = SUM(ISNULL(Debit, 0)),
        Credit         = SUM(ISNULL(Credit, 0)),
        Balance        = SUM(ISNULL(OpeningBalance, 0))
                        + SUM(ISNULL(Debit, 0))
                        - SUM(ISNULL(Credit, 0))
    FROM GlLines
    GROUP BY HeadId
),

------------------------------------------------------------
-- D) Main COA rows + Item rows
------------------------------------------------------------
FinalRows AS (

    -- Original COA heads
    SELECT
        c.Id         AS HeadId,
        c.HeadCode,
        c.HeadName,
        c.ParentHead,
        c.HeadType,
        cr.RootHeadType,

        ISNULL(ga.OpeningBalance, 0) AS OpeningBalance,
        ISNULL(ga.Debit, 0)          AS Debit,
        ISNULL(ga.Credit, 0)         AS Credit,
        ISNULL(ga.Balance, 0)        AS Balance,
        c.IsTransaction              AS IsControl
    FROM dbo.ChartOfAccount c
    LEFT JOIN CoaRoot cr ON cr.Id = c.Id
    LEFT JOIN GlAgg   ga ON ga.HeadId = c.Id
    WHERE c.IsActive = 1

    UNION ALL

    -- Item rows as children of their BudgetLine account
    SELECT
        HeadId       = -i.Id,
        HeadCode     = -i.Id,
        HeadName     = i.ItemName,
        ParentHead   = c.HeadCode,
        HeadType     = c.HeadType,
        RootHeadType = cr.RootHeadType,
        OpeningBalance = 0,
        Debit          = 0,
        Credit         = 0,
        Balance        = 0,
        IsControl      = 0
    FROM dbo.Item i
    INNER JOIN dbo.ChartOfAccount c ON c.Id = i.BudgetLineId
    INNER JOIN CoaRoot cr           ON cr.Id = c.Id
    WHERE i.IsActive = 1
)

------------------------------------------------------------
-- E) Final result
------------------------------------------------------------
SELECT
    HeadId,
    HeadCode,
    HeadName,
    ParentHead,
    HeadType,
    RootHeadType,
    OpeningBalance,
    Debit,
    Credit,
    Balance,
    IsControl
FROM FinalRows
ORDER BY HeadCode;
";

            return await Connection.QueryAsync<GeneralLedgerDTO>(query);
        }





    }
}
