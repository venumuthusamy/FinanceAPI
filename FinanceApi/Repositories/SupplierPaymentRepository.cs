using System.Data;
using Dapper;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace FinanceApi.Data
{
    public class SupplierPaymentRepository : ISupplierPaymentRepository
    {
        private readonly IConfiguration _config;
        private IDbConnection Connection => new SqlConnection(_config.GetConnectionString("DefaultConnection"));

        public SupplierPaymentRepository(IConfiguration config)
        {
            _config = config;
        }

        // Generate PaymentNo: PM-000001, PM-000002...
        private async Task<string> GeneratePaymentNoAsync(IDbConnection conn)
        {
            const string sql = @"
DECLARE @Next INT;
SELECT @Next = ISNULL(MAX(CAST(SUBSTRING(PaymentNo, 4, 10) AS INT)), 0) + 1
FROM dbo.SupplierPayment WITH (UPDLOCK);

SELECT 'PM-' + RIGHT('000000' + CAST(@Next AS VARCHAR(6)), 6);";

            return await conn.ExecuteScalarAsync<string>(sql);
        }

        // ===== LIST ALL PAYMENTS =====
        public async Task<IEnumerable<SupplierPaymentDTO>> GetAllAsync()
        {
            const string sql = @"
SELECT
    sp.Id,
    sp.PaymentNo,
    sp.SupplierId,
    s.Name AS SupplierName,
    sp.SupplierInvoiceId,
    pin.InvoiceNo,
    sp.PaymentDate,
    sp.PaymentMethodId,
    CASE sp.PaymentMethodId
        WHEN 1 THEN 'Cash'
        WHEN 2 THEN 'Bank Transfer'
        WHEN 3 THEN 'Cheque'
        ELSE 'Other'
    END AS PaymentMethodName,
    sp.ReferenceNo,
    sp.Amount,
    sp.Notes,
    sp.Status
FROM dbo.SupplierPayment      sp
LEFT JOIN dbo.Suppliers       s   ON s.Id  = sp.SupplierId
LEFT JOIN dbo.SupplierInvoicePin pin ON pin.Id = sp.SupplierInvoiceId
WHERE sp.IsActive = 1
ORDER BY sp.PaymentDate DESC, sp.Id DESC;";

            using var conn = Connection;
            return await conn.QueryAsync<SupplierPaymentDTO>(sql);
        }

        // ===== LIST PAYMENTS BY SUPPLIER =====
        public async Task<IEnumerable<SupplierPaymentDTO>> GetBySupplierAsync(int supplierId)
        {
            const string sql = @"
SELECT
    sp.Id,
    sp.PaymentNo,
    sp.SupplierId,
    s.Name AS SupplierName,
    sp.SupplierInvoiceId,
    pin.InvoiceNo,
    sp.PaymentDate,
    sp.PaymentMethodId,
    CASE sp.PaymentMethodId
        WHEN 1 THEN 'Cash'
        WHEN 2 THEN 'Bank Transfer'
        WHEN 3 THEN 'Cheque'
        ELSE 'Other'
    END AS PaymentMethodName,
    sp.ReferenceNo,
    sp.Amount,
    sp.Notes,
    sp.Status
FROM dbo.SupplierPayment      sp
LEFT JOIN dbo.Suppliers       s   ON s.Id  = sp.SupplierId
LEFT JOIN dbo.SupplierInvoicePin pin ON pin.Id = sp.SupplierInvoiceId
WHERE sp.IsActive = 1
  AND sp.SupplierId = @SupplierId
ORDER BY sp.PaymentDate DESC, sp.Id DESC;";

            using var conn = Connection;
            return await conn.QueryAsync<SupplierPaymentDTO>(sql, new { SupplierId = supplierId });
        }

        // ===== CREATE PAYMENT (no manual conn.Open, no tx) =====
        public async Task<bool> CreateAsync(SupplierPaymentCreateDTO dto)
        {
            using var conn = Connection;

            // 1) Generate the new payment number
            var paymentNo = await GeneratePaymentNoAsync(conn);

            // 2) Insert payment row
            const string insertSql = @"
INSERT INTO dbo.SupplierPayment
(
    PaymentNo,
    SupplierId,
    SupplierInvoiceId,
    PaymentDate,
    PaymentMethodId,
    ReferenceNo,
    Amount,
    Notes,
    BankId,       -- 🔹 NEW COLUMN
    Status,
    CreatedBy,
    CreatedDate,
    IsActive
)
VALUES
(
    @PaymentNo,
    @SupplierId,
    @SupplierInvoiceId,
    @PaymentDate,
    @PaymentMethodId,
    @ReferenceNo,
    @Amount,
    @Notes,
    @BankId,      -- 🔹 NEW PARAM
    1,            -- 1 = Posted
    @CreatedBy,
    SYSDATETIME(),
    1
);";

            var affected = await conn.ExecuteAsync(insertSql, new
            {
                PaymentNo = paymentNo,
                dto.SupplierId,
                dto.SupplierInvoiceId,
                dto.PaymentDate,
                dto.PaymentMethodId,
                dto.ReferenceNo,
                dto.Amount,
                dto.Notes,
                dto.BankId,       // 🔹 pass through
                dto.CreatedBy
            });

            return affected > 0;
        }

    }
}
