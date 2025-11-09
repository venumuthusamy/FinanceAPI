using Dapper;
using FinanceApi.Data;
using System.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.Data.SqlClient;

namespace FinanceApi.Repositories
{
    public class SalesOrderRepository : DynamicRepository, ISalesOrderRepository
    {
        private readonly ApplicationDbContext _context;

        public SalesOrderRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
        {

        }

        public async Task<IEnumerable<SalesOrderDTO>> GetAllAsync()
        {
            const string headersSql = @"
SELECT
    so.Id,
    so.SalesOrderNo,
    so.QuotationNo, 
    so.CustomerId,
    ISNULL(c.CustomerName,'')   AS CustomerName,
>>>>>>>>> Temporary merge branch 2
    so.RequestedDate,
    so.DeliveryDate,
    so.Status,
    so.Shipping,
    so.Discount,
    so.GstPct,
    so.CreatedBy,
    so.CreatedDate,
    so.UpdatedBy,
    so.UpdatedDate,
    so.IsActive
FROM SalesOrder so
LEFT JOIN Customer  c ON so.CustomerId = c.Id
WHERE so.IsActive = 1
ORDER BY so.Id;";

            var headers = (await Connection.QueryAsync<SalesOrderDTO>(headersSql)).ToList();
            if (headers.Count == 0) return headers;

            var ids = headers.Select(h => h.Id).ToArray();

            const string linesSql = @"
SELECT
    Id,
    SalesOrderId,
    ItemId,
    ItemName,
    Uom,
    Quantity,
    UnitPrice,
    Discount,
    Tax,
    Total,
    CreatedBy,
    CreatedDate,
    UpdatedBy,
    UpdatedDate,
    IsActive
FROM SalesOrderLines
WHERE SalesOrderId IN @Ids AND IsActive = 1;";

            var lines = await Connection.QueryAsync<SalesOrderLines>(linesSql, new { Ids = ids });

            var byId = headers.ToDictionary(h => h.Id);
            foreach (var ln in lines)
            {
                if (byId.TryGetValue(ln.SalesOrederId, out var parent))
                    parent.LineItems.Add(ln);
            }

            return headers;
        }

        public async Task<SalesOrderDTO?> GetByIdAsync(int id)
        {
            const string headerSql = @"
SELECT TOP (1)
    so.Id,
    so.QuotationNo, 
    so.CustomerId,
<<<<<<<<< Temporary merge branch 1
    ISNULL(c.Name,'')   AS CustomerName,
    so.RequestedDate,
=========
    ISNULL(c.CustomerName,'')   AS CustomerName,
     so.RequestedDate,
>>>>>>>>> Temporary merge branch 2
    so.DeliveryDate,
    so.Status,
    so.Shipping,
    so.Discount,
    so.GstPct,
    so.CreatedBy,
    so.CreatedDate,
    so.UpdatedBy,
    so.UpdatedDate,
    so.IsActive
FROM SalesOrder so

LEFT JOIN Customer  c ON so.CustomerId = c.Id
WHERE so.Id = @Id AND so.IsActive = 1;";

            var header = await Connection.QueryFirstOrDefaultAsync<SalesOrderDTO>(headerSql, new { Id = id });
            if (header == null) return null;

            const string linesSql = @"
SELECT
    s.Id,
    s.SalesOrderId,
    s.ItemId,
    s.ItemName,
    s.Uom,
	u.Name as UomName,
    s.Quantity,
    s.UnitPrice,
    s.Discount,
    s.Tax,
    s.Total,
    s.CreatedBy,
    s.CreatedDate,
    s.UpdatedBy,
    s.UpdatedDate,
    s.IsActive
FROM SalesOrderLines as s 
inner join Uom as u on u.Id = s.Uom
WHERE SalesOrderId = @Id AND s.IsActive = 1
ORDER BY s.Id;";

            var lines = await Connection.QueryAsync<SalesOrderLinesList>(linesSql, new { Id = id });
            header.LineItemsList = lines.ToList();

            return header;
        }

        public async Task<int> CreateAsync(SalesOrder so)
        {
            if (so is null) throw new ArgumentNullException(nameof(so));

            var now = DateTime.UtcNow;
            if (so.CreatedDate == default) so.CreatedDate = now;
            if (so.UpdatedDate == null) so.UpdatedDate = now;

            const string insertHeaderSql = @"
INSERT INTO SalesOrder
(
    QuotationNo, CustomerId, WarehouseId, RequestedDate, DeliveryDate,
    Status, Shipping, Discount, GstPct,
    CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
)
OUTPUT INSERTED.Id
VALUES
(
    @QuotationNo, @CustomerId, @WarehouseId, @RequestedDate, @DeliveryDate,
    @Status, @Shipping, @Discount, @GstPct,
    @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, @IsActive
);";

            const string insertLineSql = @"
INSERT INTO SalesOrderLines
(
    SalesOrderId, ItemId, ItemName, Uom, Quantity, UnitPrice, Discount, Tax, Total,
    CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
)
VALUES
(
    @SalesOrderId, @ItemId, @ItemName, @Uom, @Quantity, @UnitPrice, @Discount, @Tax, @Total,
    @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, 1
);";

            var conn = Connection;
            if (conn.State != ConnectionState.Open)
                await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                var newId = await conn.ExecuteScalarAsync<int>(
                    insertHeaderSql,
                    new
                    {
                        so.QuotationNo,
                        so.CustomerId,
                        so.WarehouseId,
                        so.RequestedDate,
                        so.DeliveryDate,
                        so.Status,
                        so.Shipping,
                        so.Discount,
                        so.GstPct,
                        so.CreatedBy,
                        so.CreatedDate,
                        so.UpdatedBy,
                        UpdatedDate = so.UpdatedDate ?? now,
                        IsActive = so.IsActive
                    },
                    transaction: tx
                );

                if (so.LineItems?.Count > 0)
                {
                    var lineParams = so.LineItems.Select(l => new
                    {
                        SalesOrderId = newId,
                        l.ItemId,
                        l.ItemName,
                        l.Uom,
                        l.Quantity,
                        l.UnitPrice,
                        l.Discount,
                        l.Tax,
                        l.Total,
                        CreatedBy = so.CreatedBy,
                        CreatedDate = so.CreatedDate,
                        UpdatedBy = so.UpdatedBy,
                        UpdatedDate = so.UpdatedDate ?? now
                    });

                    await conn.ExecuteAsync(insertLineSql, lineParams, transaction: tx);
                }

                tx.Commit();
                return newId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task UpdateAsync(SalesOrder so)
        {
            if (so is null) throw new ArgumentNullException(nameof(so));

            var now = DateTime.UtcNow;
            so.UpdatedDate = now;

            const string updateHeaderSql = @"
UPDATE SalesOrder
SET
    QuotationNo  = @QuotationNo,
    CustomerId   = @CustomerId,
    WarehouseId  = @WarehouseId,
    RequestedDate= @RequestedDate,
    DeliveryDate = @DeliveryDate,
    Status       = @Status,
    Shipping     = @Shipping,
    Discount     = @Discount,
    GstPct       = @GstPct,
    UpdatedBy    = @UpdatedBy,
    UpdatedDate  = @UpdatedDate
WHERE Id = @Id;";

            const string updateLineSql = @"
UPDATE SalesOrderLines
SET
    ItemId     = @ItemId,
    ItemName   = @ItemName,
    Uom        = @Uom,
    Quantity   = @Quantity,
    UnitPrice  = @UnitPrice,
    Discount   = @Discount,
    Tax        = @Tax,
    Total      = @Total,
    UpdatedBy  = @UpdatedBy,
    UpdatedDate= @UpdatedDate,
    IsActive   = 1
WHERE Id = @Id AND SalesOrderId = @SalesOrderId;";

            const string insertLineSql = @"
INSERT INTO SalesOrderLines
(
    SalesOrderId, ItemId, ItemName, Uom, Quantity, UnitPrice, Discount, Tax, Total,
    CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
)
VALUES
(
    @SalesOrderId, @ItemId, @ItemName, @Uom, @Quantity, @UnitPrice, @Discount, @Tax, @Total,
    @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, 1
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            const string softDeleteMissingSql = @"
UPDATE SalesOrderLines
SET IsActive = 0, UpdatedBy = @UpdatedBy, UpdatedDate = @UpdatedDate
WHERE SalesOrderId = @SalesOrderId
  AND IsActive = 1
  AND (@KeepIdsCount = 0 OR Id NOT IN @KeepIds);";

            var conn = Connection;
            if (conn.State != ConnectionState.Open)
                await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                // header
                await conn.ExecuteAsync(updateHeaderSql, new
                {
                    so.QuotationNo,
                    so.CustomerId,
                    so.WarehouseId,
                    so.RequestedDate,
                    so.DeliveryDate,
                    so.Status,
                    so.Shipping,
                    so.Discount,
                    so.GstPct,
                    so.UpdatedBy,
                    so.UpdatedDate,
                    so.Id
                }, tx);

                // lines upsert
                var keepIds = new List<int>();

                if (so.LineItems?.Count > 0)
                {
                    foreach (var l in so.LineItems)
                    {
                        if (l.Id > 0)
                        {
                            await conn.ExecuteAsync(updateLineSql, new
                            {
                                l.Id,
                                SalesOrderId = so.Id,
                                l.ItemId,
                                l.ItemName,
                                l.Uom,
                                l.Quantity,
                                l.UnitPrice,
                                l.Discount,
                                l.Tax,
                                l.Total,
                                UpdatedBy = so.UpdatedBy,
                                UpdatedDate = now
                            }, tx);

                            keepIds.Add(l.Id);
                        }
                        else
                        {
                            var newLineId = await conn.ExecuteScalarAsync<int>(insertLineSql, new
                            {
                                SalesOrderId = so.Id,
                                l.ItemId,
                                l.ItemName,
                                l.Uom,
                                l.Quantity,
                                l.UnitPrice,
                                l.Discount,
                                l.Tax,
                                l.Total,
                                CreatedBy = so.UpdatedBy ?? so.CreatedBy,
                                CreatedDate = now,
                                UpdatedBy = so.UpdatedBy,
                                UpdatedDate = now
                            }, tx);

                            keepIds.Add(newLineId);
                        }
                    }
                }

                var keepIdsParam = keepIds.Count == 0 ? new[] { -1 } : keepIds.ToArray();

                await conn.ExecuteAsync(softDeleteMissingSql, new
                {
                    SalesOrderId = so.Id,
                    KeepIds = keepIdsParam,
                    KeepIdsCount = keepIds.Count,
                    UpdatedBy = so.UpdatedBy,
                    UpdatedDate = now
                }, tx);

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task DeactivateAsync(int id, int updatedBy)
        {
            const string sqlHeader = @"
UPDATE SalesOrder
SET IsActive = 0, UpdatedBy = @UpdatedBy, UpdatedDate = SYSUTCDATETIME()
WHERE Id = @Id;";

            const string sqlLines = @"
UPDATE SalesOrderLines
SET IsActive = 0, UpdatedBy = @UpdatedBy, UpdatedDate = SYSUTCDATETIME()
WHERE SalesOrderId = @Id AND IsActive = 1;";

            var conn = Connection;
            if (conn.State != ConnectionState.Open)
                await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                var affectedHeader = await conn.ExecuteAsync(sqlHeader, new { Id = id, UpdatedBy = updatedBy }, tx);
                if (affectedHeader == 0)
                    throw new KeyNotFoundException("Sales Order not found.");

                await conn.ExecuteAsync(sqlLines, new { Id = id, UpdatedBy = updatedBy }, tx);

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }



    }
}
