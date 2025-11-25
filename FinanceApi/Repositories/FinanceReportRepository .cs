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

        public async Task<IEnumerable<TrialBalanceDTO>> GetTrialBalanceAsync(ReportBaseDTO dto)
        {
            const string sql = @"
;WITH GlLines AS (
    --------------------------------------------------------
    -- A) Manual Journal → ChartOfAccount
    --------------------------------------------------------
    SELECT
        mj.JournalDate                             AS TransDate,
        mj.AccountId                               AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        ISNULL(TRY_CONVERT(decimal(18,2), mj.Debit),  0) AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), mj.Credit), 0) AS Credit
    FROM dbo.ManualJournal mj
    INNER JOIN dbo.ChartOfAccount coa
        ON coa.Id = mj.AccountId
    WHERE mj.IsActive = 1
      AND mj.IsPosted = 1
      AND mj.JournalDate BETWEEN ISNULL(@FromDate, '1900-01-01')
                             AND ISNULL(@ToDate,   '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- B) Sales Invoice lines → Revenue account
    --------------------------------------------------------
    SELECT
        si.InvoiceDate                             AS TransDate,
        coa.Id                                     AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        CAST(0 AS decimal(18,2))                   AS Debit,
        ISNULL(TRY_CONVERT(decimal(18,2), sil.LineAmount), 0) +
        ISNULL(TRY_CONVERT(decimal(18,2), sil.Tax),         0) AS Credit
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
    WHERE si.InvoiceDate BETWEEN ISNULL(@FromDate, '1900-01-01')
                             AND ISNULL(@ToDate,   '9999-12-31')

    UNION ALL

    --------------------------------------------------------
    -- C) Credit Note → reverse revenue
    --------------------------------------------------------
    SELECT
        cn.CreditNoteDate                          AS TransDate,
        coa.Id                                     AS HeadId,
        coa.HeadCode,
        coa.HeadName,
        ISNULL(TRY_CONVERT(decimal(18,2), cnl.LineNet), 0) +
        ISNULL(TRY_CONVERT(decimal(18,2), cnl.Tax),     0) AS Debit,
        CAST(0 AS decimal(18,2))                   AS Credit
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
    WHERE cn.CreditNoteDate BETWEEN ISNULL(@FromDate, '1900-01-01')
                                AND ISNULL(@ToDate,   '9999-12-31')
),
Summ AS (
    SELECT
        HeadId,
        HeadCode,
        HeadName,
        SUM(ISNULL(Debit,0))  AS Debit,
        SUM(ISNULL(Credit,0)) AS Credit
    FROM GlLines
    GROUP BY HeadId, HeadCode, HeadName
)
SELECT
    HeadId,
    HeadCode,
    HeadName,
    Debit,
    Credit
FROM Summ
ORDER BY HeadCode;
";

            return await Connection.QueryAsync<TrialBalanceDTO>(sql, new
            {
                dto.FromDate,
                dto.ToDate,
                dto.CompanyId
            });
        }


    }

}
