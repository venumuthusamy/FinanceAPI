using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using Microsoft.Data.SqlClient;
using UnityWorksERP.Finance.AR;

namespace FinanceApi.Repositories
{
    public class ArReceiptRepository : DynamicRepository, IArReceiptRepository
    {
        public ArReceiptRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        #region LIST / DETAIL / OPEN INVOICES

        public async Task<IEnumerable<ArReceiptListDto>> GetListAsync()
        {
            const string sql = @"
SELECT
    r.Id,
    r.ReceiptNo,
    r.CustomerId,
    ISNULL(c.CustomerName, '') AS CustomerName,
    r.ReceiptDate,
    r.PaymentMode,
    r.BankId,
    --ISNULL(b.BankName, '') AS BankName,
    r.AmountReceived,
    r.TotalAllocated,
    r.Unallocated,
    r.Status
FROM dbo.ArReceipt r
LEFT JOIN dbo.Customer    c ON c.Id = r.CustomerId
--LEFT JOIN dbo.BankAccount b ON b.Id = r.BankId
WHERE r.IsActive = 1
ORDER BY r.ReceiptDate DESC, r.Id DESC;";

            return await Connection.QueryAsync<ArReceiptListDto>(sql);
        }

        public async Task<ArReceiptDetailDto?> GetByIdAsync(int id)
        {
            const string hdrSql = @"
SELECT
    r.Id,
    r.ReceiptNo,
    r.CustomerId,
    ISNULL(c.CustomerName, '') AS CustomerName,
    r.ReceiptDate,
    r.PaymentMode,
    r.BankId,
    r.AmountReceived,
    r.TotalAllocated,
    r.Unallocated,
    r.ReferenceNo,
    r.Remarks,
    r.Status
FROM dbo.ArReceipt r
LEFT JOIN dbo.Customer c ON c.Id = r.CustomerId
WHERE r.Id = @Id AND r.IsActive = 1;";

            const string lineSql = @"
SELECT
    ra.Id,
    ra.InvoiceId,
    ISNULL(si.InvoiceNo, '')  AS InvoiceNo,
    si.InvoiceDate            AS InvoiceDate,
    ISNULL(si.Total, 0)       AS Amount,       -- or si.GrandTotal if that’s your field

    ISNULL(pay.PaidAmount,0)  AS PaidAmount,   -- total paid for this invoice
    ISNULL(si.Total,0) - ISNULL(pay.PaidAmount,0) AS Balance,

    ra.AllocatedAmount        AS AllocatedAmount
FROM dbo.ArReceiptAllocation ra
INNER JOIN dbo.salesinvoice si
        ON si.Id = ra.InvoiceId
OUTER APPLY
(
    SELECT SUM(ra2.AllocatedAmount) AS PaidAmount
    FROM dbo.ArReceiptAllocation ra2
    INNER JOIN dbo.ArReceipt r2
        ON r2.Id = ra2.ReceiptId AND r2.IsActive = 1
    WHERE ra2.InvoiceId = ra.InvoiceId
      AND ra2.IsActive = 1
) pay
WHERE ra.ReceiptId = @Id
  AND ra.IsActive = 1
ORDER BY ra.Id;";

            using var multi = await Connection.QueryMultipleAsync(
                $"{hdrSql} {lineSql}", new { Id = id });

            var header = await multi.ReadFirstOrDefaultAsync<ArReceiptDetailDto>();
            if (header == null) return null;

            header.Allocations = (await multi.ReadAsync<ArReceiptAllocationDto>()).ToList();
            return header;
        }


        public async Task<IEnumerable<SalesInvoiceOpenDto>> GetOpenInvoicesAsync(int customerId)
        {
            const string sql = @"
SELECT
    Id,
    InvoiceNo,
    InvoiceDate,
    Amount,
    PaidAmount,
    Balance
FROM dbo.vwSalesInvoiceOpenForReceipt
WHERE CustomerId = @CustomerId
  AND Balance > 0
ORDER BY InvoiceDate;";

            return await Connection.QueryAsync<SalesInvoiceOpenDto>(
                sql, new { CustomerId = customerId });
        }

        #endregion

        #region CREATE / UPDATE / DELETE

        public async Task<int> CreateAsync(ArReceiptCreateUpdateDto dto, int userId)
        {
            // ==== open connection like PickingRepository ====
            var conn = Connection;
            if (conn.State != ConnectionState.Open)
                await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                // Generate next receipt number
                const string nextNoSql = @"
DECLARE @Next INT = (SELECT ISNULL(MAX(Id),0) + 1 FROM dbo.ArReceipt);
SELECT 'RC-' + FORMAT(@Next, '000000');";

                var receiptNo = await conn.ExecuteScalarAsync<string>(
                    nextNoSql, transaction: tx);

                var totalAllocated = dto.Allocations.Sum(a => a.AllocatedAmount);
                var unallocated = dto.AmountReceived - totalAllocated;

                const string insertHdrSql = @"
INSERT INTO dbo.ArReceipt
(
    ReceiptNo, CustomerId, ReceiptDate,
    PaymentMode, BankId,
    AmountReceived, TotalAllocated, Unallocated,
    ReferenceNo, Remarks,
    Status, IsActive,
    CreatedBy
)
VALUES
(
    @ReceiptNo, @CustomerId, @ReceiptDate,
    @PaymentMode, @BankId,
    @AmountReceived, @TotalAllocated, @Unallocated,
    @ReferenceNo, @Remarks,
    'Posted', 1,
    @CreatedBy
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

                var receiptId = await conn.ExecuteScalarAsync<int>(
                    insertHdrSql,
                    new
                    {
                        ReceiptNo = receiptNo,
                        dto.CustomerId,
                        dto.ReceiptDate,
                        dto.PaymentMode,
                        dto.BankId,
                        dto.AmountReceived,
                        TotalAllocated = totalAllocated,
                        Unallocated = unallocated,
                        dto.ReferenceNo,
                        dto.Remarks,
                        CreatedBy = userId
                    },
                    transaction: tx);

                const string insertLineSql = @"
INSERT INTO dbo.ArReceiptAllocation
(
    ReceiptId, InvoiceId, AllocatedAmount,
    IsActive, CreatedBy
)
VALUES
(
    @ReceiptId, @InvoiceId, @AllocatedAmount,
    1, @CreatedBy
);";

                foreach (var line in dto.Allocations.Where(a => a.AllocatedAmount > 0))
                {
                    await conn.ExecuteAsync(
                        insertLineSql,
                        new
                        {
                            ReceiptId = receiptId,
                            line.InvoiceId,
                            line.AllocatedAmount,
                            CreatedBy = userId
                        },
                        transaction: tx);
                }

                tx.Commit();
                return receiptId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task UpdateAsync(ArReceiptCreateUpdateDto dto, int userId)
        {
            if (!dto.Id.HasValue)
                throw new ArgumentException("Id is required for update.", nameof(dto));

            var conn = Connection;
            if (conn.State != ConnectionState.Open)
                await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                var totalAllocated = dto.Allocations.Sum(a => a.AllocatedAmount);
                var unallocated = dto.AmountReceived - totalAllocated;

                const string updateHdrSql = @"
UPDATE dbo.ArReceipt
SET CustomerId     = @CustomerId,
    ReceiptDate    = @ReceiptDate,
    PaymentMode    = @PaymentMode,
    BankId         = @BankId,
    AmountReceived = @AmountReceived,
    TotalAllocated = @TotalAllocated,
    Unallocated    = @Unallocated,
    ReferenceNo    = @ReferenceNo,
    Remarks        = @Remarks,
    UpdatedBy      = @UpdatedBy,
    UpdatedDate    = SYSUTCDATETIME()
WHERE Id = @Id AND IsActive = 1;";

                await conn.ExecuteAsync(
                    updateHdrSql,
                    new
                    {
                        dto.Id,
                        dto.CustomerId,
                        dto.ReceiptDate,
                        dto.PaymentMode,
                        dto.BankId,
                        dto.AmountReceived,
                        TotalAllocated = totalAllocated,
                        Unallocated = unallocated,
                        dto.ReferenceNo,
                        dto.Remarks,
                        UpdatedBy = userId
                    },
                    transaction: tx);

                // delete existing lines
                const string deleteLinesSql = @"
DELETE FROM dbo.ArReceiptAllocation WHERE ReceiptId = @ReceiptId;";

                await conn.ExecuteAsync(
                    deleteLinesSql,
                    new { ReceiptId = dto.Id.Value },
                    transaction: tx);

                // re-insert lines
                const string insertLineSql = @"
INSERT INTO dbo.ArReceiptAllocation
(
    ReceiptId, InvoiceId, AllocatedAmount,
    IsActive, CreatedBy
)
VALUES
(
    @ReceiptId, @InvoiceId, @AllocatedAmount,
    1, @CreatedBy
);";

                foreach (var line in dto.Allocations.Where(a => a.AllocatedAmount > 0))
                {
                    await conn.ExecuteAsync(
                        insertLineSql,
                        new
                        {
                            ReceiptId = dto.Id.Value,
                            line.InvoiceId,
                            line.AllocatedAmount,
                            CreatedBy = userId
                        },
                        transaction: tx);
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task DeleteAsync(int id, int userId)
        {
            var conn = Connection;
            if (conn.State != ConnectionState.Open)
                await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                const string sql = @"
UPDATE dbo.ArReceipt
SET IsActive    = 0,
    UpdatedBy   = @UserId,
    UpdatedDate = SYSUTCDATETIME()
WHERE Id = @Id;

UPDATE dbo.ArReceiptAllocation
SET IsActive    = 0,
    UpdatedBy   = @UserId,
    UpdatedDate = SYSUTCDATETIME()
WHERE ReceiptId = @Id;";

                await conn.ExecuteAsync(sql, new { Id = id, UserId = userId }, tx);

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        #endregion
    }
}
