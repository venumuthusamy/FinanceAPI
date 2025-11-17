using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Repositories
{
    public class JournalRepository : DynamicRepository, IJournalRepository
    {
        public JournalRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
        {

        }


        public async Task<IEnumerable<JournalsDTO>> GetAllAsync()
        {
            var sql = @"
SELECT 
    'SI' AS RowType,                  -- SalesInvoice row
    si.Id           AS SalesInvoiceId,
    si.InvoiceNo,
    si.InvoiceDate,
    NULL            AS JournalId,
    NULL            AS JournalNo,
    NULL            AS JournalDate,
    coa.HeadName,
    coa.HeadCode,
    -- Amount for Sales Invoice
    SUM((sil.Qty * sil.UnitPrice) * (1 - (sil.DiscountPct / 100.0))) AS Amount,
    -- No debit for SI row
    NULL AS DebitAmount
FROM dbo.SalesInvoice si
LEFT JOIN dbo.SalesInvoiceLine sil ON sil.SiId = si.Id
INNER JOIN dbo.Item item ON item.Id = sil.ItemId
INNER JOIN dbo.ChartOfAccount AS coa ON coa.Id = item.BudgetLineId 
WHERE si.IsActive = 1
GROUP BY 
    si.Id, si.InvoiceNo, si.InvoiceDate,
    coa.HeadName, coa.HeadCode

UNION ALL

SELECT
    'MJ'           AS RowType,
    NULL           AS SalesInvoiceId,
    NULL           AS InvoiceNo,
    NULL           AS InvoiceDate,
    mj.Id          AS JournalId,
    mj.JournalNo,
    mj.JournalDate,
    coa.HeadName,
    coa.HeadCode,
    -- Amount column – here we use Credit
    mj.Credit      AS Amount,
    -- Extra column for debit
    mj.Debit       AS DebitAmount
FROM dbo.ManualJournal mj
INNER JOIN dbo.ChartOfAccount coa
    ON CAST(mj.AccountId AS VARCHAR(50)) = coa.HeadCode
WHERE mj.IsActive = 1

ORDER BY 
    HeadCode, 
    RowType,
    InvoiceDate,
    JournalDate;


";
            return await Connection.QueryAsync<JournalsDTO>(sql);
        }



        public async Task<int> CreateAsync(ManualJournalCreateDto dto)
        {
            const string sql = @"
INSERT INTO dbo.ManualJournal
(
    AccountId,
    JournalDate,
    Type,
    CustomerId,
    SupplierId,
    Description,
    Debit,
    Credit,
    IsRecurring,
    RecurringFrequency,
    RecurringInterval,
    RecurringStartDate,
    RecurringEndType,
    RecurringEndDate,
    RecurringCount,
    ProcessedCount,
    NextRunDate,
    CreatedBy,
    CreatedDate,
    IsActive
)
VALUES
(
    @AccountId,
    @JournalDate,
    @Type,
    @CustomerId,
    @SupplierId,
    @Description,
    @Debit,
    @Credit,
    @IsRecurring,
    @RecurringFrequency,
    @RecurringInterval,
    @RecurringStartDate,
    @RecurringEndType,
    @RecurringEndDate,
    @RecurringCount,
    0,   -- ProcessedCount
    CASE 
        WHEN @IsRecurring = 1 
             THEN ISNULL(@RecurringStartDate, @JournalDate)
        ELSE NULL
    END,
    @CreatedBy,
    SYSUTCDATETIME(),
    1
);

SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var id = await Connection.ExecuteScalarAsync<int>(sql, dto);
            return id;
        }

        public async Task<IEnumerable<ManualJournalDto>> GetAllRecurringDetails()
        {
            const string sql = @"
SELECT
    mj.Id,
    mj.JournalNo,
    mj.AccountId,
    coa.HeadName AS AccountName,
    mj.JournalDate,
    mj.Type,
    mj.CustomerId,
    c.CustomerName,
    mj.SupplierId,
    s.Name AS SupplierName,
    mj.Description,
    mj.Debit,
    mj.Credit,
    mj.IsRecurring,
    mj.RecurringFrequency,
    mj.RecurringInterval,
    mj.RecurringStartDate,
    mj.RecurringEndType,
    mj.RecurringEndDate,
    mj.RecurringCount,
    mj.ProcessedCount,
    mj.NextRunDate
FROM dbo.ManualJournal mj
LEFT JOIN dbo.ChartOfAccount coa ON coa.Id = mj.AccountId
LEFT JOIN dbo.Customer c ON c.Id = mj.CustomerId
LEFT JOIN dbo.Supplier s ON s.Id = mj.SupplierId
WHERE mj.IsActive = 1
ORDER BY mj.JournalDate DESC, mj.Id DESC;";

            var rows = await Connection.QueryAsync<ManualJournalDto>(sql);
            return rows;
        }


        public async Task<ManualJournalDto?> GetByIdAsync(int id)
        {
            const string sql = @"
SELECT
    mj.Id,
    mj.JournalNo,
    mj.AccountId,
    coa.HeadName AS AccountName,
    mj.JournalDate,
    mj.Type,
    mj.CustomerId,
    c.CustomerName,
    mj.SupplierId,
    s.Name AS SupplierName,
    mj.Description,
    mj.Debit,
    mj.Credit,
    mj.IsRecurring,
    mj.RecurringFrequency,
    mj.RecurringInterval,
    mj.RecurringStartDate,
    mj.RecurringEndType,
    mj.RecurringEndDate,
    mj.RecurringCount,
    mj.ProcessedCount,
    mj.NextRunDate
FROM dbo.ManualJournal mj
LEFT JOIN dbo.ChartOfAccount coa ON coa.Id = mj.AccountId
LEFT JOIN dbo.Customer c ON c.Id = mj.CustomerId
LEFT JOIN dbo.Supplier s ON s.Id = mj.SupplierId
WHERE mj.Id = @Id AND mj.IsActive = 1;";

            return await Connection.QueryFirstOrDefaultAsync<ManualJournalDto>(sql, new { Id = id });
        }


        public async Task<int> ProcessRecurringAsync(DateTime processDate)
        {
            // 1) Get all recurring templates that should run today
            const string selectSql = @"
SELECT
    mj.Id,
    mj.JournalNo,
    mj.AccountId,
    mj.JournalDate,
    mj.Type,
    mj.CustomerId,
    mj.SupplierId,
    mj.Description,
    mj.Debit,
    mj.Credit,
    mj.IsRecurring,
    mj.RecurringFrequency,
    mj.RecurringInterval,
    mj.RecurringStartDate,
    mj.RecurringEndType,
    mj.RecurringEndDate,
    mj.RecurringCount,
    mj.ProcessedCount,
    mj.NextRunDate,
    mj.CreatedBy
FROM dbo.ManualJournal mj
WHERE mj.IsActive = 1
  AND mj.IsRecurring = 1
  AND mj.NextRunDate IS NOT NULL
  AND mj.NextRunDate <= @Today
  AND (
        mj.RecurringEndType = 'NoEnd'
        OR (mj.RecurringEndType = 'EndByDate' 
            AND mj.RecurringEndDate IS NOT NULL 
            AND mj.NextRunDate <= mj.RecurringEndDate)
        OR (mj.RecurringEndType = 'EndByCount'
            AND mj.RecurringCount IS NOT NULL
            AND mj.ProcessedCount < mj.RecurringCount)
      );";

            var templates = (await Connection.QueryAsync<TemplateRow>(selectSql, new { Today = processDate.Date })).ToList();

            if (templates.Count == 0)
                return 0;

            // 2) For each template, insert a new journal entry + update template
            const string insertSql = @"
INSERT INTO dbo.ManualJournal
(
    AccountId,
    JournalDate,
    Type,
    CustomerId,
    SupplierId,
    Description,
    Debit,
    Credit,
    IsRecurring,
    RecurringFrequency,
    RecurringInterval,
    RecurringStartDate,
    RecurringEndType,
    RecurringEndDate,
    RecurringCount,
    ProcessedCount,
    NextRunDate,
    CreatedBy,
    CreatedDate,
    IsActive
)
VALUES
(
    @AccountId,
    @JournalDate,
    @Type,
    @CustomerId,
    @SupplierId,
    @Description,
    @Debit,
    @Credit,
    0,              -- generated entry is NOT recurring
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    0,
    NULL,
    @CreatedBy,
    SYSUTCDATETIME(),
    1
);";

            const string updateSql = @"
UPDATE dbo.ManualJournal
SET ProcessedCount = @ProcessedCount,
    NextRunDate    = @NextRunDate,
    UpdatedDate    = SYSUTCDATETIME()
WHERE Id = @Id;";

            using var tran = Connection.BeginTransaction();
            try
            {
                int generatedCount = 0;

                foreach (var t in templates)
                {
                    // 2.1 Insert new journal for this run date
                    var newEntryParams = new
                    {
                        AccountId = t.AccountId,
                        JournalDate = t.NextRunDate,       // the scheduled date
                        Type = t.Type,
                        CustomerId = t.CustomerId,
                        SupplierId = t.SupplierId,
                        Description = t.Description,
                        Debit = t.Debit,
                        Credit = t.Credit,
                        CreatedBy = t.CreatedBy
                    };

                    await Connection.ExecuteAsync(insertSql, newEntryParams, tran);
                    generatedCount++;

                    // 2.2 Compute next run date
                    var nextDate = GetNextRunDate(t.NextRunDate!.Value, t.RecurringFrequency, t.RecurringInterval);
                    var newProcessedCount = t.ProcessedCount + 1;

                    // If end type is by count and we reached the limit, you can set NextRunDate = NULL
                    if (t.RecurringEndType == "EndByCount"
                        && t.RecurringCount.HasValue
                        && newProcessedCount >= t.RecurringCount.Value)
                    {
                        nextDate = null;
                    }

                    var updateParams = new
                    {
                        Id = t.Id,
                        ProcessedCount = newProcessedCount,
                        NextRunDate = (object?)nextDate
                    };

                    await Connection.ExecuteAsync(updateSql, updateParams, tran);
                }

                tran.Commit();
                return generatedCount;
            }
            catch
            {
                tran.Rollback();
                throw;
            }
        }

        // Helper class just for this method
        private class TemplateRow
        {
            public int Id { get; set; }
            public int AccountId { get; set; }
            public string? Type { get; set; }
            public int? CustomerId { get; set; }
            public int? SupplierId { get; set; }
            public string? Description { get; set; }
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
            public bool IsRecurring { get; set; }
            public string? RecurringFrequency { get; set; }
            public int? RecurringInterval { get; set; }
            public DateTime? RecurringStartDate { get; set; }
            public string? RecurringEndType { get; set; }
            public DateTime? RecurringEndDate { get; set; }
            public int? RecurringCount { get; set; }
            public int ProcessedCount { get; set; }
            public DateTime? NextRunDate { get; set; }
            public int? CreatedBy { get; set; }
        }

        private DateTime? GetNextRunDate(DateTime current, string? freq, int? interval)
        {
            if (string.IsNullOrEmpty(freq))
                return null;

            var step = (interval.HasValue && interval.Value > 0) ? interval.Value : 1;

            return freq switch
            {
                "Daily" => current.AddDays(step),
                "Weekly" => current.AddDays(7 * step),
                "Monthly" => current.AddMonths(step),
                "Yearly" => current.AddYears(step),
                _ => (DateTime?)null
            };
        }
    }
}
