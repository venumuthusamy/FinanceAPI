// Repositories/SupplierInvoiceRepository.cs
using System.Data;
using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Repositories
{
    public class SupplierInvoicePinRepository : DynamicRepository, ISupplierInvoicePinRepository
    {
        public SupplierInvoicePinRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory) { }

        // ---------- READ ----------
        public async Task<IEnumerable<SupplierInvoicePin>> GetAllAsync()
        {
            const string sql = @"
SELECT *
FROM dbo.SupplierInvoicePin
WHERE IsActive = 1
ORDER BY Id DESC;";

            return await Connection.QueryAsync<SupplierInvoicePin>(sql);
        }

        public async Task<SupplierInvoicePinDTO> GetByIdAsync(int id)
        {
            const string sql = @"
  SELECT si.*,grp.GrnNo FROM SupplierInvoicePin as si
  inner join PurchaseGoodReceipt as grp on grp.Id = si.grnid
  WHERE si.Id = @Id;";
            return await Connection.QuerySingleAsync<SupplierInvoicePinDTO>(sql, new { Id = id });
        }

        // ---------- CREATE ----------
        public async Task<int> CreateAsync(SupplierInvoicePin pin)
        {
            // Generate next PIN-#### similar to PR repo style
            const string getLastNo = @"
SELECT TOP 1 InvoiceNo
FROM dbo.SupplierInvoicePin
WHERE ISNUMERIC(SUBSTRING(InvoiceNo, 5, LEN(InvoiceNo))) = 1
ORDER BY Id DESC;";

            var last = await Connection.QueryFirstOrDefaultAsync<string>(getLastNo);

            int next = 1;
            if (!string.IsNullOrWhiteSpace(last) && last.StartsWith("PIN-"))
            {
                var numeric = last.Substring(4);
                if (int.TryParse(numeric, out var n)) next = n + 1;
            }

            pin.InvoiceNo = $"PIN-{next:D4}";
            pin.CreatedDate = pin.UpdatedDate = DateTime.UtcNow;

            const string insert = @"
INSERT INTO dbo.SupplierInvoicePin
(InvoiceNo, InvoiceDate, Amount, Tax, Currency, Status, LinesJson,
 IsActive, CreatedDate, UpdatedDate, CreatedBy, UpdatedBy, GrnId)
OUTPUT INSERTED.Id
VALUES
(@InvoiceNo, @InvoiceDate, @Amount, @Tax, @Currency, @Status, @LinesJson,
 1, @CreatedDate, @UpdatedDate, @CreatedBy, @UpdatedBy, @GrnId);";

            return await Connection.QueryFirstAsync<int>(insert, pin);
        }

        // ---------- UPDATE ----------
        public async Task UpdateAsync(SupplierInvoicePin pin)
        {
            pin.UpdatedDate = DateTime.UtcNow;

            const string update = @"
UPDATE dbo.SupplierInvoicePin SET
  InvoiceDate = @InvoiceDate,
  Amount      = @Amount,
  Tax         = @Tax,
  Currency    = @Currency,
  Status      = @Status,
  LinesJson   = @LinesJson,
  GrnId       = @GrnId,
  UpdatedDate = @UpdatedDate,
  UpdatedBy   = @UpdatedBy
WHERE Id = @Id;";

            await Connection.ExecuteAsync(update, pin);
        }

        // ---------- DEACTIVATE ----------
        public async Task DeactivateAsync(int id)
        {
            const string sql = @"UPDATE dbo.SupplierInvoicePin SET IsActive = 0 WHERE Id = @Id;";
            await Connection.ExecuteAsync(sql, new { Id = id });
        }
    }
}
