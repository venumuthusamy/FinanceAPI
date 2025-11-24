using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using System.Data;

namespace FinanceApi.Repositories
{
    public class JournalRepository : DynamicRepository, IJournalRepository
    {
        public JournalRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        #region LIST / DETAILS

        public async Task<IEnumerable<JournalsDTO>> GetAllAsync()
        {
            const string sql = @"
SELECT 
    mj.Id,
    mj.JournalNo, 
    mj.JournalDate,
    mj.Credit AS Amount,
    mj.Debit  AS DebitAmount, 
    mj.IsRecurring,
    mj.RecurringFrequency,
    mj.isPosted,
    coa.HeadName,
    coa.HeadCode 
FROM ManualJournal AS mj
INNER JOIN ChartOfAccount AS coa ON coa.Id = mj.AccountId
WHERE mj.IsActive = 1 and mj.isPosted =0;";

            return await Connection.QueryAsync<JournalsDTO>(sql);
        }


        public async Task<int> MarkAsPostedAsync(IEnumerable<int> ids)
        {
            const string sql = @"
UPDATE dbo.ManualJournal
SET 
    isPosted   = 1,
    UpdatedDate = SYSUTCDATETIME()
WHERE Id IN @Ids
  AND IsActive = 1
  AND isPosted = 0;";

            return await Connection.ExecuteAsync(sql, new { Ids = ids });
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
    IsActive,
isPosted
)
VALUES
(
    @AccountId,
    @JournalDateUtc,         -- UTC
    @Type,
    @CustomerId,
    @SupplierId,
    @Description,
    @Debit,
    @Credit,
    @IsRecurring,
    @RecurringFrequency,
    @RecurringInterval,
    @RecurringStartDateUtc, 
    @RecurringEndType,
    @RecurringEndDateUtc,    
    @RecurringCount,
    0,                     
    CASE 
        WHEN @IsRecurring = 1 THEN
            COALESCE(@RecurringStartDateUtc, @JournalDateUtc)
        ELSE NULL
    END,
    @CreatedBy,
    SYSUTCDATETIME(),
    1,
0
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

        #endregion

        #region PROCESS RECURRING

        public async Task<int> ProcessRecurringAsync(DateTime processUtc)
        {
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
  AND mj.NextRunDate <= @NowUtc;";

            var templates = (await Connection.QueryAsync<TemplateRow>(selectSql, new { NowUtc = processUtc }))
                           .ToList();

            if (templates.Count == 0)
                return 0;

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
    0,
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
                    // 1. amount split for EndByCount
                    decimal debitToPost = t.Debit;
                    decimal creditToPost = t.Credit;

                    if (t.RecurringEndType == "EndByCount"
                        && t.RecurringCount.HasValue
                        && t.RecurringCount.Value > 0)
                    {
                        int totalInstallments = t.RecurringCount.Value;
                        int currentInstallment = t.ProcessedCount + 1;

                        var baseDebit = Math.Round(t.Debit / totalInstallments, 2, MidpointRounding.AwayFromZero);
                        var baseCredit = Math.Round(t.Credit / totalInstallments, 2, MidpointRounding.AwayFromZero);

                        if (currentInstallment < totalInstallments)
                        {
                            debitToPost = baseDebit;
                            creditToPost = baseCredit;
                        }
                        else
                        {
                            debitToPost = t.Debit - baseDebit * (totalInstallments - 1);
                            creditToPost = t.Credit - baseCredit * (totalInstallments - 1);
                        }
                    }

                    // 2. Insert generated row (JournalDate = NextRunDate)
                    var newEntryParams = new
                    {
                        AccountId = t.AccountId,
                        JournalDate = t.NextRunDate!.Value, // UTC
                        Type = t.Type,
                        CustomerId = t.CustomerId,
                        SupplierId = t.SupplierId,
                        Description = t.Description,
                        Debit = debitToPost,
                        Credit = creditToPost,
                        CreatedBy = t.CreatedBy
                    };

                    await Connection.ExecuteAsync(insertSql, newEntryParams, tran);
                    generatedCount++;

                    // 3. compute next run
                    var nextDate = GetNextRunDate(t.NextRunDate!.Value, t.RecurringFrequency, t.RecurringInterval);
                    var newProcessedCount = t.ProcessedCount + 1;

                    // EndByCount stop
                    if (t.RecurringEndType == "EndByCount"
                        && t.RecurringCount.HasValue
                        && newProcessedCount >= t.RecurringCount.Value)
                    {
                        nextDate = null;
                    }

                    // EndByDate stop
                    if (t.RecurringEndType == "EndByDate"
                        && t.RecurringEndDate.HasValue
                        && nextDate.HasValue
                        && nextDate.Value.Date > t.RecurringEndDate.Value.Date)
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

        #endregion

        #region Helper

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

        private DateTime? GetNextRunDate(DateTime currentUtc, string? freq, int? interval)
        {
            if (string.IsNullOrEmpty(freq))
                return null;

            var step = (interval.HasValue && interval.Value > 0) ? interval.Value : 1;

            return freq switch
            {
                "Daily" => currentUtc.AddDays(step),
                "Weekly" => currentUtc.AddDays(7 * step),
                "Monthly" => currentUtc.AddMonths(step),
                "Quarterly" => currentUtc.AddMonths(3 * step),
                "Yearly" => currentUtc.AddYears(step),
                "EveryMinute" => currentUtc.AddMinutes(step),
                _ => (DateTime?)null
            };
        }

        #endregion
    }
}
