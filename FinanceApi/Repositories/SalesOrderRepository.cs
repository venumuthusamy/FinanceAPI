// SalesOrderRepository.cs
using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;
using static FinanceApi.ModelDTO.QutationDetailsViewInfo;

namespace FinanceApi.Repositories
{
    public class SalesOrderRepository : DynamicRepository, ISalesOrderRepository
    {
        public SalesOrderRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

        public async Task<IEnumerable<SalesOrderDTO>> GetAllAsync()
        {
            const string headersSql = @"
SELECT
    so.Id, so.QuotationNo, so.CustomerId,
    ISNULL(c.CustomerName,'') AS CustomerName,
    so.RequestedDate, so.DeliveryDate, so.Status,
    so.Shipping, so.Discount, so.GstPct,
    so.CreatedBy, so.CreatedDate, so.UpdatedBy, so.UpdatedDate, so.IsActive, so.SalesOrderNo
FROM dbo.SalesOrder so
LEFT JOIN dbo.Customer c ON c.Id = so.CustomerId
WHERE so.IsActive = 1
ORDER BY so.Id;";

            var headers = (await Connection.QueryAsync<SalesOrderDTO>(headersSql)).ToList();
            if (headers.Count == 0) return headers;

            var ids = headers.Select(h => h.Id).ToArray();

            const string linesSql = @"
SELECT
    Id, SalesOrderId, ItemId, ItemName, Uom,
    Quantity, UnitPrice, Discount, Tax, Total,
    WarehouseId, BinId, Available, SupplierId,
    CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
FROM dbo.SalesOrderLines
WHERE SalesOrderId IN @Ids AND IsActive = 1;";

            var lines = await Connection.QueryAsync<SalesOrderLineDTO>(linesSql, new { Ids = ids });
            var map = headers.ToDictionary(h => h.Id);
            foreach (var ln in lines)
                if (map.TryGetValue(ln.SalesOrderId, out var parent)) parent.LineItems.Add(ln);

            return headers;
        }

        public async Task<SalesOrderDTO?> GetByIdAsync(int id)
        {
            const string headerSql = @"
SELECT TOP(1)
    so.Id, so.QuotationNo, so.CustomerId,
    ISNULL(c.CustomerName,'') AS CustomerName,
    so.RequestedDate, so.DeliveryDate, so.Status,
    so.Shipping, so.Discount, so.GstPct,
    so.CreatedBy, so.CreatedDate, so.UpdatedBy, so.UpdatedDate, so.IsActive, so.SalesOrderNo
FROM dbo.SalesOrder so
LEFT JOIN dbo.Customer c ON c.Id = so.CustomerId
WHERE so.Id = @Id AND so.IsActive = 1;";

            var head = await Connection.QueryFirstOrDefaultAsync<SalesOrderDTO>(headerSql, new { Id = id });
            if (head is null) return null;

            const string linesSql = @"
SELECT
    Id, SalesOrderId, ItemId, ItemName, Uom,
    Quantity, UnitPrice, Discount, Tax, Total,
    WarehouseId, BinId, Available, SupplierId,
    CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
FROM dbo.SalesOrderLines
WHERE SalesOrderId = @Id AND IsActive = 1
ORDER BY Id;";

            var lines = await Connection.QueryAsync<SalesOrderLineDTO>(linesSql, new { Id = id });
            head.LineItems = lines.ToList();
            return head;
        }

        public async Task<int> CreateAsync(SalesOrder so)
        {
            var now = DateTime.UtcNow;
            if (so.CreatedDate == default) so.CreatedDate = now;
            if (so.UpdatedDate == null) so.UpdatedDate = now;

            const string insertHeader = @"
INSERT INTO dbo.SalesOrder
(QuotationNo, CustomerId, RequestedDate, DeliveryDate, Status, Shipping, Discount, GstPct,
 SalesOrderNo, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive)
OUTPUT INSERTED.Id
VALUES
(@QuotationNo, @CustomerId, @RequestedDate, @DeliveryDate, @Status, @Shipping, @Discount, @GstPct,
 @SalesOrderNo, @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, 1);";

            const string insertLine = @"
INSERT INTO dbo.SalesOrderLines
(SalesOrderId, ItemId, ItemName, Uom, Quantity, UnitPrice, Discount, Tax, Total,
 WarehouseId, BinId, Available, SupplierId,
 CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive)
VALUES
(@SalesOrderId, @ItemId, @ItemName, @Uom, @Quantity, @UnitPrice, @Discount, @Tax, @Total,
 @WarehouseId, @BinId, @Available, @SupplierId,
 @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, 1);";

            const string getItemMasterId = @"
SELECT TOP 1 im.Id
FROM dbo.Item i
JOIN dbo.ItemMaster im ON im.Sku = i.ItemCode
WHERE i.Id = @ItemId;";

            const string getBinAvail = @"
SELECT TOP 1 iws.BinId, iws.Available
FROM dbo.ItemWarehouseStock iws
WHERE iws.ItemId = @ItemMasterId AND iws.WarehouseId = @WarehouseId
ORDER BY iws.Available DESC;";

            const string getSupplier = @"
SELECT TOP 1 ip.SupplierId
FROM dbo.ItemPrice ip
WHERE ip.ItemId = @ItemMasterId AND ip.WarehouseId = @WarehouseId
ORDER BY ip.Id ASC;";

            var conn = Connection;
            if (conn.State != ConnectionState.Open)  
                await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();
            try
            {
                // Generate running SO number
                var soNo = await GetNextSalesOrderNoAsync(conn, tx, "SO-", 4);

                // Insert Header
                var salesOrderId = await conn.ExecuteScalarAsync<int>(insertHeader, new
                {
                    so.QuotationNo,
                    so.CustomerId,
                    so.RequestedDate,
                    so.DeliveryDate,
                    so.Status,
                    so.Shipping,
                    so.Discount,
                    so.GstPct,
                    SalesOrderNo = soNo,
                    so.CreatedBy,
                    so.CreatedDate,
                    so.UpdatedBy,
                    UpdatedDate = so.UpdatedDate ?? now
                }, tx);

                // Insert Line Items
                foreach (var l in so.LineItems)
                {
                    var whId = l.SelectedWarehouseId ?? l.WarehouseId;

                    var itemMasterId = await conn.ExecuteScalarAsync<int?>(getItemMasterId,
                        new { l.ItemId }, tx) ?? 0;

                    int? binId = null;
                    decimal? available = null;
                    int? supplierId = null;

                    if (itemMasterId > 0 && whId.HasValue)
                    {
                        var ba = await conn.QueryFirstOrDefaultAsync<(int? BinId, decimal? Available)>(
                            getBinAvail, new { ItemMasterId = itemMasterId, WarehouseId = whId.Value }, tx);

                        binId = ba.BinId;
                        available = ba.Available;

                        supplierId = await conn.ExecuteScalarAsync<int?>(
                            getSupplier, new { ItemMasterId = itemMasterId, WarehouseId = whId.Value }, tx);
                    }

                    await conn.ExecuteAsync(insertLine, new
                    {
                        SalesOrderId = salesOrderId,
                        l.ItemId,
                        l.ItemName,
                        Uom = l.Uom,                // ✅ Store UOM Name
                        l.Quantity,
                        l.UnitPrice,
                        l.Discount,
                        Tax = l.Tax,                     // ✅ No conversion (string stays string)
                        l.Total,
                        WarehouseId = whId,
                        BinId = binId,
                        Available = available,
                        SupplierId = supplierId,
                        so.CreatedBy,
                        so.CreatedDate,
                        so.UpdatedBy,
                        UpdatedDate = so.UpdatedDate ?? now
                    }, tx);
                }

                tx.Commit();
                return salesOrderId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }



        private async Task<string> GetNextSalesOrderNoAsync(
    IDbConnection conn,
    IDbTransaction tx,
    string prefix = "SO-",
    int width = 4)
        {
            // Lock the table while we read the current max,
            // so concurrent requests don’t pick the same number.
            const string sql = @"
DECLARE @n INT;

-- Lock the read (acts like SERIALIZABLE on this table scan)
SELECT @n = ISNULL(MAX(TRY_CONVERT(int, RIGHT(SalesOrderNo, @Width))), 0) + 1
FROM dbo.SalesOrder WITH (UPDLOCK, HOLDLOCK);

SELECT @n;
";

            var next = await conn.ExecuteScalarAsync<int>(
                sql,
                new { Width = width },
                transaction: tx);

            // Format: SO-0001
            return $"{prefix}{next.ToString().PadLeft(width, '0')}";
        }

        public async Task UpdateAsync(SalesOrder so)
        {
            var now = DateTime.UtcNow;

            const string updHead = @"
UPDATE dbo.SalesOrder SET
    QuotationNo=@QuotationNo, CustomerId=@CustomerId, RequestedDate=@RequestedDate, DeliveryDate=@DeliveryDate,
    Status=@Status, Shipping=@Shipping, Discount=@Discount, GstPct=@GstPct,
    UpdatedBy=@UpdatedBy, UpdatedDate=@UpdatedDate
WHERE Id=@Id;";

            const string updLine = @"
UPDATE dbo.SalesOrderLines SET
    ItemId=@ItemId, ItemName=@ItemName, Uom=@Uom, Quantity=@Quantity,
    UnitPrice=@UnitPrice, Discount=@Discount, Tax=@Tax, Total=@Total,
    WarehouseId=@WarehouseId, BinId=@BinId, Available=@Available, SupplierId=@SupplierId,
    UpdatedBy=@UpdatedBy, UpdatedDate=@UpdatedDate, IsActive=1
WHERE Id=@Id AND SalesOrderId=@SalesOrderId;";

            const string insLine = @"
INSERT INTO dbo.SalesOrderLines
(SalesOrderId, ItemId, ItemName, Uom, Quantity, UnitPrice, Discount, Tax, Total,
 WarehouseId, BinId, Available, SupplierId,
 CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive)
VALUES
(@SalesOrderId, @ItemId, @ItemName, @Uom, @Quantity, @UnitPrice, @Discount, @Tax, @Total,
 @WarehouseId, @BinId, @Available, @SupplierId,
 @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, 1);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            const string softDeleteMissing = @"
UPDATE dbo.SalesOrderLines
SET IsActive=0, UpdatedBy=@UpdatedBy, UpdatedDate=@UpdatedDate
WHERE SalesOrderId=@SalesOrderId AND IsActive=1
  AND (@KeepIdsCount=0 OR Id NOT IN @KeepIds);";

            const string getItemMasterId = @"
SELECT TOP 1 im.Id
FROM dbo.Item i
JOIN dbo.ItemMaster im ON im.Sku = i.ItemCode
WHERE i.Id = @ItemId;";

            const string getBinAvail = @"
SELECT TOP 1 iws.BinId, iws.Available
FROM dbo.ItemWarehouseStock iws
WHERE iws.ItemId = @ItemMasterId AND iws.WarehouseId = @WarehouseId
ORDER BY iws.Available DESC;";

            const string getSupplier = @"
SELECT TOP 1 ip.SupplierId
FROM dbo.ItemPrice ip
WHERE ip.ItemId = @ItemMasterId AND ip.WarehouseId = @WarehouseId
ORDER BY ip.Qty ASC, ip.Id ASC;";

            const string getItemName = @"SELECT TOP 1 ItemName FROM dbo.Item WHERE Id = @ItemId;";

            var conn = Connection;
            if (conn.State != ConnectionState.Open) await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();
            try
            {
                await conn.ExecuteAsync(updHead, new
                {
                    so.QuotationNo,
                    so.CustomerId,
                    so.RequestedDate,
                    so.DeliveryDate,
                    so.Status,
                    so.Shipping,
                    so.Discount,
                    so.GstPct,
                    so.UpdatedBy,
                    UpdatedDate = now,
                    so.Id
                }, tx);

                var keepIds = new List<int>();

                if (so.LineItems?.Count > 0)
                {
                    foreach (var l in so.LineItems)
                    {
                        var whId = l.SelectedWarehouseId ?? l.WarehouseId;

                        var itemMasterId = await conn.ExecuteScalarAsync<int?>(getItemMasterId, new { l.ItemId }, tx) ?? 0;

                        int? binId = null;
                        decimal? available = null;
                        int? supplierId = null;

                        if (itemMasterId > 0 && whId.HasValue)
                        {
                            var ba = await conn.QueryFirstOrDefaultAsync<(int? BinId, decimal? Available)>(
                                getBinAvail, new { ItemMasterId = itemMasterId, WarehouseId = whId.Value }, tx);
                            binId = ba.BinId;
                            available = ba.Available;

                            supplierId = await conn.ExecuteScalarAsync<int?>(
                                getSupplier, new { ItemMasterId = itemMasterId, WarehouseId = whId.Value }, tx);
                        }

                        var itemName = await conn.ExecuteScalarAsync<string?>(getItemName, new { l.ItemId }, tx) ?? "";

                        if (l.Id > 0)
                        {
                            await conn.ExecuteAsync(updLine, new
                            {
                                l.Id,
                                SalesOrderId = so.Id,
                                l.ItemId,
                                ItemName = itemName,
                                Uom = l.Uom,
                                l.Quantity,
                                l.UnitPrice,
                                l.Discount,
                                l.Tax,
                                l.Total,
                                WarehouseId = whId,
                                BinId = binId,
                                Available = available,
                                SupplierId = supplierId,
                                UpdatedBy = so.UpdatedBy,
                                UpdatedDate = now
                            }, tx);
                            keepIds.Add(l.Id);
                        }
                        else
                        {
                            var newLineId = await conn.ExecuteScalarAsync<int>(insLine, new
                            {
                                SalesOrderId = so.Id,
                                l.ItemId,
                                ItemName = itemName,
                                Uom = l.Uom,
                                l.Quantity,
                                l.UnitPrice,
                                l.Discount,
                                l.Tax,
                                l.Total,
                                WarehouseId = whId,
                                BinId = binId,
                                Available = available,
                                SupplierId = supplierId,
                                CreatedBy = so.UpdatedBy ?? so.CreatedBy,
                                CreatedDate = now,
                                UpdatedBy = so.UpdatedBy,
                                UpdatedDate = now
                            }, tx);
                            keepIds.Add(newLineId);
                        }
                    }
                }

                await conn.ExecuteAsync(softDeleteMissing, new
                {
                    SalesOrderId = so.Id,
                    KeepIds = keepIds.Count == 0 ? new[] { -1 } : keepIds.ToArray(),
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
            const string sqlHead = @"UPDATE dbo.SalesOrder SET IsActive=0, UpdatedBy=@UpdatedBy, UpdatedDate=SYSUTCDATETIME() WHERE Id=@Id;";
            const string sqlLines = @"UPDATE dbo.SalesOrderLines SET IsActive=0, UpdatedBy=@UpdatedBy, UpdatedDate=SYSUTCDATETIME() WHERE SalesOrderId=@Id AND IsActive=1;";

            var conn = Connection;
            if (conn.State != ConnectionState.Open) await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();
            try
            {
                var n = await conn.ExecuteAsync(sqlHead, new { Id = id, UpdatedBy = updatedBy }, tx);
                if (n == 0) throw new KeyNotFoundException("Sales Order not found.");
                await conn.ExecuteAsync(sqlLines, new { Id = id, UpdatedBy = updatedBy }, tx);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task<QutationDetailsViewInfo?> GetByQuatitonDetails(int id)
        {
            const string sql = @"
SELECT q.Id,q.Number,q.Status,q.CustomerId,c.CustomerName AS CustomerName,
       q.CurrencyId,q.FxRate,q.PaymentTermsId,q.ValidityDate,
       q.Subtotal,q.TaxAmount,q.Rounding,q.GrandTotal,q.NeedsHodApproval,
       cu.CurrencyName,pt.PaymentTermsName,q.ValidityDate
FROM dbo.Quotation q
LEFT JOIN dbo.Customer     c  ON c.Id=q.CustomerId
LEFT JOIN dbo.Currency     cu ON cu.Id = q.CurrencyId
LEFT JOIN dbo.PaymentTerms pt ON pt.Id = q.PaymentTermsId
WHERE q.Id= @Id AND q.IsActive=1;

SELECT l.Id,
       l.QuotationId,
       l.ItemId,
       i.ItemName AS ItemName,
       l.UomId,
       u.Name AS UomName,
       l.Qty,
       l.UnitPrice,
       l.DiscountPct,
       l.TaxMode,
       l.LineNet,
       l.LineTax,
       l.LineTotal,
       whAgg.WarehouseCount,
       whAgg.WarehouseIdsCsv AS WarehouseIds,
       whAgg.WarehousesJson
FROM dbo.QuotationLine l
LEFT JOIN dbo.Item i  ON i.Id = l.ItemId
LEFT JOIN dbo.Uom  u  ON u.Id = l.UomId
OUTER APPLY (
    SELECT im.Id AS ItemMasterId
    FROM dbo.ItemMaster im
    WHERE im.Sku = i.ItemCode
) AS IMX
OUTER APPLY (
    SELECT
      COUNT(DISTINCT W.WarehouseId) AS WarehouseCount,
      STRING_AGG(CAST(W.WarehouseId AS varchar(20)), ',') AS WarehouseIdsCsv,
      (
        SELECT 
            W.WarehouseId,
            wh.Name as WarehouseName,
            SUM(W.OnHand)   AS OnHand,
            SUM(W.Reserved) AS Reserved,
            SUM(W.OnHand - W.Reserved) AS Available
        FROM dbo.ItemWarehouseStock W
        JOIN dbo.Warehouse wh ON wh.Id = W.WarehouseId
        WHERE W.ItemId = IMX.ItemMasterId
        GROUP BY W.WarehouseId, wh.Name
        FOR JSON PATH
      ) AS WarehousesJson
    FROM dbo.ItemWarehouseStock W
    WHERE W.ItemId = IMX.ItemMasterId
) AS whAgg
WHERE l.QuotationId = @Id
ORDER BY l.Id;";

            using var multi = await Connection.QueryMultipleAsync(sql, new { Id = id });

            var head = await multi.ReadFirstOrDefaultAsync<QutationDetailsViewInfo>();
            if (head is null) return null;

            var lines = (await multi.ReadAsync<QutationDetailsViewInfo.QuotationLineDetailsViewInfo>()).ToList();

            foreach (var l in lines)
            {
                if (!string.IsNullOrWhiteSpace(l.WarehousesJson))
                {
                    try
                    {
                        l.Warehouses = JsonSerializer.Deserialize<List<QutationDetailsViewInfo.WarehouseInfoDTO>>(l.WarehousesJson) ?? new();
                    }
                    catch { l.Warehouses = new(); }
                }
                else l.Warehouses = new();
            }

            head.Lines = lines;
            return head;
        }
    }
}
