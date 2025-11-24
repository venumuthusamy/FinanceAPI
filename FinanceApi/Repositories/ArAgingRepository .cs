using FinanceApi.Data;
using FinanceApi.Repositories;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using System.Data;
using Dapper;

public class ArAgingRepository : DynamicRepository, IArAgingRepository
{
    public ArAgingRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public async Task<IEnumerable<ArAgingSummaryDto>> GetSummaryAsync(DateTime asOfDate)
    {
        var param = new { AsOfDate = asOfDate };
        return await Connection.QueryAsync<ArAgingSummaryDto>(
            "sp_ArAgingSummary",
            param,
            commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<ArAgingInvoiceDto>> GetCustomerInvoicesAsync(int customerId, DateTime asOfDate)
    {
        const string sql = @"
WITH Base AS (
    SELECT
        b.*,
        AgeRaw = DATEDIFF(DAY, b.DueDate, @AsOfDate)
    FROM dbo.vwArAgingBase b
),
Norm AS (
    SELECT
        *,
        AgeDays =
            CASE WHEN AgeRaw < 0 THEN 0 ELSE AgeRaw END,
        BucketName =
            CASE 
                WHEN AgeRaw < 0              THEN '0-30'
                WHEN AgeRaw BETWEEN 0 AND 30 THEN '0-30'
                WHEN AgeRaw BETWEEN 31 AND 60 THEN '31-60'
                WHEN AgeRaw BETWEEN 61 AND 90 THEN '61-90'
                ELSE '90+'
            END
    FROM Base
)
SELECT
    InvoiceId,
    InvoiceNo,
    InvoiceDate,
    DueDate,
    AgeDays,
    BucketName,
    CustomerId,
    CustomerName,
    OriginalAmount,
    PaidAmount,
    CreditAmount,
    Balance
FROM Norm
WHERE CustomerId = @CustomerId
ORDER BY DueDate;
";

        return await Connection.QueryAsync<ArAgingInvoiceDto>(
            sql,
            new { CustomerId = customerId, AsOfDate = asOfDate });
    }
}
