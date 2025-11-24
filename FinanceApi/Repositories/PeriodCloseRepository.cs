using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using System.Data;

namespace FinanceApi.Repositories
{
    public class PeriodCloseRepository : DynamicRepository, IPeriodCloseRepository
    {
        public PeriodCloseRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        public async Task<IEnumerable<PeriodOptionDto>> GetPeriodsAsync()
        {
            return await Connection.QueryAsync<PeriodOptionDto>(
                "dbo.sp_PeriodClose_GetPeriods",
                commandType: CommandType.StoredProcedure);
        }

        public async Task<PeriodStatusDto?> GetStatusAsync(int periodId)
        {
            return await Connection.QueryFirstOrDefaultAsync<PeriodStatusDto>(
                "dbo.sp_PeriodClose_GetStatus",
                new { PeriodId = periodId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<PeriodStatusDto> SetLockAsync(int periodId, bool lockFlag, int userId)
        {
            return await Connection.QuerySingleAsync<PeriodStatusDto>(
                "dbo.sp_PeriodClose_SetLock",
                new { PeriodId = periodId, Lock = lockFlag, UserId = userId },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<int> RunFxRevalAsync(FxRevalRequestDto req, int userId)
        {
            return await Connection.ExecuteScalarAsync<int>(
                "dbo.sp_PeriodClose_RunFxReval",
                new
                {
                    PeriodId = req.PeriodId,
                    FxDate = req.FxDate,
                    UserId = userId
                },
                commandType: CommandType.StoredProcedure);
        }
        public async Task EnsureOpenAsync(DateTime transDate)
        {
            const string sql = @"
SELECT TOP (1)
    Id,
    -- if PeriodName is null, fallback to 'MMM yyyy'
    ISNULL(PeriodName, FORMAT(StartDate, 'MMM yyyy')) AS PeriodName,
    IsLocked
FROM dbo.AccountingPeriod
WHERE
    IsActive = 1
    AND @TransDate >= StartDate
    AND @TransDate <= EndDate
ORDER BY StartDate DESC;";

            using var conn = Connection;

            var period = await conn.QueryFirstOrDefaultAsync<AccountingPeriodRow>(
                sql,
                new { TransDate = transDate.Date });

            if (period == null)
            {
                throw new InvalidOperationException(
                    $"No accounting period defined for date {transDate:dd-MMM-yyyy}. " +
                    "Please create/open the period first.");
            }

            if (period.IsLocked)
            {
                throw new InvalidOperationException(
                    $"Accounting period '{period.PeriodName}' is locked. " +
                    "You cannot post this transaction to that period.");
            }

            // If reached here → OK to post
        }
        public async Task<AccountingPeriod?> GetByDateAsync(DateTime date)
        {
            const string sql = @"
SELECT TOP (1)
    Id,
    PeriodCode,
    PeriodName,
    StartDate,
    EndDate,
    IsLocked,
    LockDate,
    LockedBy,
    IsActive,
    CreatedBy,
    CreatedDate,
    UpdatedBy,
    UpdatedDate
FROM dbo.AccountingPeriod
WHERE @Date BETWEEN StartDate AND EndDate
  AND IsActive = 1
ORDER BY StartDate;";

            using var conn = Connection;
            return await conn.QueryFirstOrDefaultAsync<AccountingPeriod>(
                sql,
                new { Date = date.Date });
        }
    }
}


