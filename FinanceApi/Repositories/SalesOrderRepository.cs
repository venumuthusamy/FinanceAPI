// SalesOrderRepository.cs
using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;
using static FinanceApi.ModelDTO.AllocationPreviewRequest;
using static FinanceApi.ModelDTO.QutationDetailsViewInfo;

namespace FinanceApi.Repositories
{
    public class SalesOrderRepository : DynamicRepository, ISalesOrderRepository
    {
        public SalesOrderRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

        // ===================== READ LIST =====================
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
    LockedQty,
    CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
FROM dbo.SalesOrderLines
WHERE SalesOrderId IN @Ids AND IsActive = 1;";
            var lines = await Connection.QueryAsync<SalesOrderLineDTO>(linesSql, new { Ids = ids });

            var map = headers.ToDictionary(h => h.Id);
            foreach (var ln in lines)
                if (map.TryGetValue(ln.SalesOrderId, out var parent)) parent.LineItems.Add(ln);

            return headers;
        }

        // ===================== READ ONE =====================
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
    sl.Id,
    sl.SalesOrderId,
    sl.ItemId,
    sl.ItemName,
    sl.Uom,
    sl.Quantity,
    sl.UnitPrice,
    sl.Discount,
    sl.Tax,
    sl.Total,
 
    -- IDs + names
    sl.WarehouseId,
    ISNULL(w.Name, '')      AS WarehouseName,
 
    sl.SupplierId,
    ISNULL(s.Name, '')      AS SupplierName,
 
    sl.BinId,
    ISNULL(b.BinName, '') AS Bin,   -- prefer Code, fallback to Name
 
    sl.Available,
    sl.LockedQty,
    sl.CreatedBy,
    sl.CreatedDate,
    sl.UpdatedBy,
    sl.UpdatedDate,
    sl.IsActive
FROM dbo.SalesOrderLines sl
LEFT JOIN dbo.Warehouse      w ON w.Id = sl.WarehouseId
LEFT JOIN dbo.Suppliers       s ON s.Id = sl.SupplierId
LEFT JOIN dbo.Bin            b ON b.Id = sl.BinId
-- If your bins are per-warehouse, use this instead of the Bin join above:
-- LEFT JOIN dbo.WarehouseBin b ON b.Id = sl.BinId AND b.WarehouseId = sl.WarehouseId
WHERE sl.SalesOrderId = @Id
  AND sl.IsActive = 1
ORDER BY sl.Id;
";
            var lines = await Connection.QueryAsync<SalesOrderLineDTO>(linesSql, new { Id = id });
            head.LineItems = lines.ToList();
            return head;
        }

        // ====================================================
        // ============= AUTO-ALLOCATION: CREATE ==============
        // ====================================================

        // allocator helper types
        private readonly record struct AllocCandidate(int WarehouseId, int? BinId, int SupplierId, decimal WhAvail, decimal SupplierQty);
        private readonly record struct Allocation(int WarehouseId, int? BinId, int SupplierId, decimal Qty);

        // Map UI ItemId -> ItemMaster.Id (via SKU)
        private async Task<int?> GetItemMasterIdAsync(IDbConnection conn, IDbTransaction tx, int itemId)
        {
            const string sql = @"
SELECT TOP 1 im.Id
FROM dbo.Item i WITH (NOLOCK)
JOIN dbo.ItemMaster im WITH (NOLOCK) ON im.Sku = i.ItemCode
WHERE i.Id = @ItemId;";
            return await conn.ExecuteScalarAsync<int?>(sql, new { ItemId = itemId }, tx);
        }

        // Effective total available across ALL warehouses for itemMaster, subtracting LockedQty on active SOs
        private async Task<decimal> GetEffectiveTotalAvailableAsync(IDbConnection conn, IDbTransaction tx, int itemMasterId)
        {
            const string sql = @"
WITH STOCK AS (
    SELECT ISNULL(SUM(iws.OnHand - iws.Reserved),0) AS Avl
    FROM dbo.ItemWarehouseStock iws WITH (NOLOCK)
    WHERE iws.ItemId = @ItemMasterId
),
LOCKED AS (
    SELECT ISNULL(SUM(sol.LockedQty),0) AS Lck
    FROM dbo.SalesOrderLines sol WITH (NOLOCK)
    JOIN dbo.SalesOrder so  WITH (NOLOCK) ON so.Id = sol.SalesOrderId AND so.IsActive = 1
    JOIN dbo.Item i         WITH (NOLOCK) ON i.Id  = sol.ItemId
    JOIN dbo.ItemMaster im  WITH (NOLOCK) ON im.Sku = i.ItemCode
    WHERE sol.IsActive = 1
      AND im.Id = @ItemMasterId
)
SELECT CASE WHEN s.Avl - l.Lck < 0 THEN 0 ELSE s.Avl - l.Lck END
FROM STOCK s CROSS JOIN LOCKED l;";
            return await conn.ExecuteScalarAsync<decimal>(sql, new { ItemMasterId = itemMasterId }, tx);
        }

        // Pull (Warehouse, Supplier) buckets (READ-ONLY — NOLOCK)
        // Pull (Warehouse, Supplier) buckets with Effective Avl = (OnHand-Reserved) - LockedQtyAcrossActiveSO
        private async Task<List<AllocCandidate>> GetAllocCandidatesAsync(
            IDbConnection conn, IDbTransaction tx, int itemMasterId)
        {
            const string sql = @"
WITH IWS AS (
    SELECT W.WarehouseId,
           W.BinId,
           W.ItemId,
           SUM(W.OnHand)   AS OnHand,
           SUM(W.Reserved) AS Reserved
    FROM dbo.ItemWarehouseStock W WITH (NOLOCK)
    WHERE W.ItemId = @ItemMasterId
    GROUP BY W.WarehouseId, W.BinId, W.ItemId
),
LCK AS (
    SELECT sol.WarehouseId, SUM(sol.LockedQty) AS LckQty
    FROM dbo.SalesOrderLines sol WITH (NOLOCK)
    JOIN dbo.SalesOrder so  WITH (NOLOCK) ON so.Id = sol.SalesOrderId AND so.IsActive = 1
    JOIN dbo.Item i        WITH (NOLOCK) ON i.Id  = sol.ItemId
    JOIN dbo.ItemMaster im WITH (NOLOCK) ON im.Sku = i.ItemCode
    WHERE sol.IsActive = 1
      AND im.Id = @ItemMasterId
    GROUP BY sol.WarehouseId
)
SELECT 
    ip.WarehouseId,
    iws.BinId,
    ip.SupplierId,
    /* Effective WH availability */
    CASE 
      WHEN iws.ItemId IS NULL THEN 0
      ELSE 
        CASE 
          WHEN (ISNULL(iws.OnHand,0) - ISNULL(iws.Reserved,0)) - ISNULL(lck.LckQty,0) < 0 
            THEN 0 
          ELSE (ISNULL(iws.OnHand,0) - ISNULL(iws.Reserved,0)) - ISNULL(lck.LckQty,0)
        END
    END AS WhAvail,
    ip.Qty AS SupplierQty
FROM dbo.ItemPrice ip WITH (NOLOCK)
LEFT JOIN IWS iws ON iws.ItemId = ip.ItemId AND iws.WarehouseId = ip.WarehouseId
LEFT JOIN LCK lck ON lck.WarehouseId = ip.WarehouseId
WHERE ip.ItemId = @ItemMasterId
  AND ip.Qty > 0
ORDER BY 
    WhAvail DESC,
    ip.Qty DESC;";

            var rows = await conn.QueryAsync<AllocCandidate>(sql, new { ItemMasterId = itemMasterId }, tx);
            return rows.ToList();
        }


        // Choose first (WarehouseId, SupplierId, BinId): prefer row with IWS; then by Available / Qty
        private async Task<(int? WarehouseId, int? BinId, int? SupplierId)> GetFirstWarehouseSupplierAsync(
            IDbConnection conn, IDbTransaction tx, int itemMasterId)
        {
            const string sql = @"
SELECT TOP 1
    ip.WarehouseId,
    iws.BinId,
    ip.SupplierId
FROM dbo.ItemPrice ip WITH (NOLOCK)
LEFT JOIN dbo.ItemWarehouseStock iws WITH (NOLOCK)
  ON iws.ItemId = ip.ItemId AND iws.WarehouseId = ip.WarehouseId
WHERE ip.ItemId = @ItemMasterId
ORDER BY 
    CASE WHEN iws.ItemId IS NULL THEN 0 ELSE 1 END DESC,
    ISNULL(iws.Available, 0) DESC,
    ip.Qty DESC, ip.Id DESC;";
            var row = await conn.QueryFirstOrDefaultAsync(sql, new { ItemMasterId = itemMasterId }, tx);
            if (row == null) return (null, null, null);
            int? wh = (int?)row.WarehouseId;
            int? bin = (int?)row.BinId;
            int? sup = (int?)row.SupplierId;
            return (wh, bin, sup);
        }

        // Greedy allocator
        private static List<Allocation> MakeAllocation(List<AllocCandidate> cands, decimal requiredQty)
        {
            var left = requiredQty;
            var res = new List<Allocation>();
            foreach (var c in cands)
            {
                if (left <= 0) break;
                var take = Math.Min(left, Math.Min(c.WhAvail, c.SupplierQty));
                if (take <= 0) continue;
                res.Add(new Allocation(c.WarehouseId, c.BinId, c.SupplierId, take));
                left -= take;
            }
            return res;
        }

        // Insert rows only (NO stock table updates) — running remainder rows, no initial row
        private async Task ApplyAllocationWithRunningRemainderAsync(
            IDbConnection conn, IDbTransaction tx,
            int salesOrderId,
            SalesOrderLines requestLine,
            List<Allocation> allocs,
            DateTime now,
            SalesOrder so)
        {
            const string insLine = @"
INSERT INTO dbo.SalesOrderLines
(SalesOrderId, ItemId, ItemName, Uom, Quantity, UnitPrice, Discount, Tax, Total,
 WarehouseId, BinId, Available, SupplierId, LockedQty,
 CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive)
VALUES
(@SalesOrderId, @ItemId, @ItemName, @Uom, @Quantity, @UnitPrice, @Discount, @Tax, @Total,
 @WarehouseId, @BinId, @Available, @SupplierId, @LockedQty,
 @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, 1);";

            var totalQty = requestLine.Quantity;
            if (totalQty <= 0) return;

            decimal remaining = totalQty;

            foreach (var a in allocs)
            {
                var sliceQty = a.Qty;
                if (sliceQty <= 0) continue;

                // Row shows state BEFORE this allocation:
                // Quantity = remaining, Available = remaining, LockedQty = sliceQty
                var rowQty = remaining;

                var proration = totalQty > 0 ? (sliceQty / totalQty) : 0m;
                var sliceTot = Math.Round(requestLine.Total * proration, 2, MidpointRounding.AwayFromZero);

                await conn.ExecuteAsync(insLine, new
                {
                    SalesOrderId = salesOrderId,
                    ItemId = requestLine.ItemId,
                    ItemName = requestLine.ItemName,
                    Uom = requestLine.Uom,

                    Quantity = rowQty,
                    UnitPrice = requestLine.UnitPrice,
                    Discount = requestLine.Discount,
                    Tax = requestLine.Tax,
                    Total = sliceTot,

                    WarehouseId = a.WarehouseId,
                    BinId = a.BinId,
                    Available = rowQty,
                    SupplierId = a.SupplierId,
                    LockedQty = sliceQty,

                    CreatedBy = so.CreatedBy,
                    CreatedDate = so.CreatedDate == default ? now : so.CreatedDate,
                    UpdatedBy = so.UpdatedBy ?? so.CreatedBy,
                    UpdatedDate = so.UpdatedDate ?? now
                }, tx);

                remaining -= sliceQty;
                if (remaining < 0) remaining = 0;
            }

            // If there's a remainder (backorder) and you want to keep a row (no WH/SUP/BIN)
            if (remaining > 0)
            {
                var prorationRem = totalQty > 0 ? (remaining / totalQty) : 0m;
                var remTot = Math.Round(requestLine.Total * prorationRem, 2, MidpointRounding.AwayFromZero);

                await conn.ExecuteAsync(insLine, new
                {
                    SalesOrderId = salesOrderId,
                    ItemId = requestLine.ItemId,
                    ItemName = requestLine.ItemName,
                    Uom = requestLine.Uom,

                    Quantity = remaining,
                    UnitPrice = requestLine.UnitPrice,
                    Discount = requestLine.Discount,
                    Tax = requestLine.Tax,
                    Total = remTot,

                    WarehouseId = (int?)null,
                    BinId = (int?)null,
                    Available = remaining,
                    SupplierId = (int?)null,
                    LockedQty = 0m,

                    CreatedBy = so.CreatedBy,
                    CreatedDate = so.CreatedDate == default ? now : so.CreatedDate,
                    UpdatedBy = so.UpdatedBy ?? so.CreatedBy,
                    UpdatedDate = so.UpdatedDate ?? now
                }, tx);
            }
        }

        // Insert a single row when effective availability == 0 (no stock anywhere)
        // Stamp first WH/SUP/BIN. LockedQty = user entered qty. Quantity/Available = 0.
        private async Task InsertNoStockLineAsync(
            IDbConnection conn, IDbTransaction tx,
            int salesOrderId,
            SalesOrderLines requestLine,
            (int? WarehouseId, int? BinId, int? SupplierId) pick,
            DateTime now,
            SalesOrder so)
        {
            const string insLine = @"
INSERT INTO dbo.SalesOrderLines
(SalesOrderId, ItemId, ItemName, Uom, Quantity, UnitPrice, Discount, Tax, Total,
 WarehouseId, BinId, Available, SupplierId, LockedQty,
 CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive)
VALUES
(@SalesOrderId, @ItemId, @ItemName, @Uom, @Quantity, @UnitPrice, @Discount, @Tax, @Total,
 @WarehouseId, @BinId, @Available, @SupplierId, @LockedQty,
 @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, 1);";

            await conn.ExecuteAsync(insLine, new
            {
                SalesOrderId = salesOrderId,
                ItemId = requestLine.ItemId,
                ItemName = requestLine.ItemName,
                Uom = requestLine.Uom,

                Quantity = 0m,
                UnitPrice = requestLine.UnitPrice,
                Discount = requestLine.Discount,
                Tax = requestLine.Tax,
                Total = requestLine.Total,

                WarehouseId = pick.WarehouseId,
                BinId = pick.BinId,
                Available = 0m,
                SupplierId = pick.SupplierId,
                LockedQty = requestLine.Quantity,   // lock the whole thing

                CreatedBy = so.CreatedBy,
                CreatedDate = now,
                UpdatedBy = so.UpdatedBy ?? so.CreatedBy,
                UpdatedDate = so.UpdatedDate ?? now
            }, tx);
        }

        // ========= Raise a purchase alert (no PR row) =========
        // in SalesOrderRepository (or wherever you call it)
        // inside SalesOrderRepository
        private async Task CreatePurchaseAlertAsync(
            IDbConnection conn, IDbTransaction tx,
            int salesOrderId, string soNo,
            int itemId, string itemName, decimal qty,
            int? warehouseId, int? supplierId)
        {
            // Resolve display names (fallback to ids)
            string? whName = warehouseId.HasValue
                ? await conn.ExecuteScalarAsync<string?>("SELECT Name FROM dbo.Warehouse WITH (NOLOCK) WHERE Id=@Id;", new { Id = warehouseId.Value }, tx)
                : null;

            string? supName = supplierId.HasValue
                ? await conn.ExecuteScalarAsync<string?>("SELECT Name FROM dbo.Suppliers WITH (NOLOCK) WHERE Id=@Id;", new { Id = supplierId.Value }, tx)
                : null;

            var itemLabel = string.IsNullOrWhiteSpace(itemName) ? $"#{itemId}" : itemName;
            var title = $"Sales shortage — Item:{itemLabel} · Qty:{qty}"
                      + (warehouseId != null ? $" · WH:{(whName ?? warehouseId.ToString())}" : "")
                      + (supplierId != null ? $" · SUP:{(supName ?? supplierId.ToString())}" : "");

            const string ins = @"
INSERT INTO dbo.PurchaseAlert
(ItemId, ItemName, RequiredQty, WarehouseId, SupplierId,
 Source, SourceId, SourceNo, Message, IsRead, CreatedDate)
VALUES
(@ItemId, @ItemName, @RequiredQty, @WarehouseId, @SupplierId,
 'SO', @SourceId, @SourceNo, @Message, 0, SYSUTCDATETIME());";

            await conn.ExecuteAsync(ins, new
            {
                ItemId = itemId,
                ItemName = (object?)itemName ?? DBNull.Value,
                RequiredQty = qty,
                WarehouseId = warehouseId,
                SupplierId = supplierId,
                SourceId = salesOrderId,
                SourceNo = soNo,
                Message = title
            }, tx);
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

            var conn = Connection;
            if (conn.State != ConnectionState.Open)
                await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();
            try
            {
                // SO running number
                var soNo = await GetNextSalesOrderNoAsync(conn, tx, "SO-", 4);

                // Insert header
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

                // For each UI line: if effective availability == 0, write a single locked row + alert.
                // Otherwise, do greedy running-remainder rows.
                foreach (var l in so.LineItems)
                {
                    var itemMasterId = await GetItemMasterIdAsync(conn, tx, l.ItemId) ?? 0;
                    if (itemMasterId == 0)
                        throw new InvalidOperationException($"Item master not found for ItemId {l.ItemId}");

                    var effAvail = await GetEffectiveTotalAvailableAsync(conn, tx, itemMasterId);

                    if (effAvail <= 0)
                    {
                        var pick = await GetFirstWarehouseSupplierAsync(conn, tx, itemMasterId);

                        // Single row, full LockedQty, Qty/Available=0
                        await InsertNoStockLineAsync(conn, tx, salesOrderId, l, pick, now, so);

                        // Raise alert for PR team
                        await CreatePurchaseAlertAsync(conn, tx, salesOrderId, soNo, l.ItemId, l.ItemName, l.Quantity, pick.WarehouseId, pick.SupplierId);

                        continue; // next line
                    }

                    // Stock exists → candidates & greedy
                    var cands = await GetAllocCandidatesAsync(conn, tx, itemMasterId);
                    var allocs = MakeAllocation(cands, l.Quantity);

                    await ApplyAllocationWithRunningRemainderAsync(conn, tx, salesOrderId, l, allocs, now, so);
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

        // ===================== PREVIEW (READ-ONLY) =====================
        public async Task<AllocationPreviewResponse> PreviewAllocationAsync(AllocationPreviewRequest req)
        {
            var result = new AllocationPreviewResponse();
            if (req?.Lines == null || req.Lines.Count == 0)
                return result;

            var conn = Connection;
            if (conn.State != ConnectionState.Open)
                await (conn as SqlConnection)!.OpenAsync();

            const string sqlMapItemMaster = @"
SELECT TOP 1 im.Id
FROM dbo.Item i WITH (NOLOCK)
JOIN dbo.ItemMaster im WITH (NOLOCK) ON im.Sku = i.ItemCode
WHERE i.Id = @ItemId;";

            const string sqlPreviewCandidates = @"
WITH IWS AS (
    SELECT W.WarehouseId,
           W.BinId,
           W.ItemId,
           SUM(W.OnHand)   AS OnHand,
           SUM(W.Reserved) AS Reserved
    FROM dbo.ItemWarehouseStock W WITH (NOLOCK)
    WHERE W.ItemId = @ItemMasterId
    GROUP BY W.WarehouseId, W.BinId, W.ItemId
),
LCK AS (
    SELECT sol.WarehouseId, SUM(sol.LockedQty) AS LckQty
    FROM dbo.SalesOrderLines sol WITH (NOLOCK)
    JOIN dbo.SalesOrder so  WITH (NOLOCK) ON so.Id = sol.SalesOrderId AND so.IsActive = 1
    JOIN dbo.Item i        WITH (NOLOCK) ON i.Id  = sol.ItemId
    JOIN dbo.ItemMaster im WITH (NOLOCK) ON im.Sku = i.ItemCode
    WHERE sol.IsActive = 1
      AND im.Id = @ItemMasterId
    GROUP BY sol.WarehouseId
)
SELECT 
    ip.WarehouseId,
    w.Name      AS WarehouseName,
    iws.BinId,
    b.BinName,
    ip.SupplierId,
    s.Name As SupplierName,
    /* Effective WH availability for preview */
    CASE 
      WHEN iws.ItemId IS NULL THEN 0
      ELSE 
        CASE 
          WHEN (ISNULL(iws.OnHand,0) - ISNULL(iws.Reserved,0)) - ISNULL(lck.LckQty,0) < 0 
            THEN 0 
          ELSE (ISNULL(iws.OnHand,0) - ISNULL(iws.Reserved,0)) - ISNULL(lck.LckQty,0)
        END
    END AS WhAvail,
    ip.Qty      AS SupplierQty
FROM dbo.ItemPrice ip WITH (NOLOCK)
LEFT JOIN IWS iws ON iws.ItemId = ip.ItemId AND iws.WarehouseId = ip.WarehouseId
LEFT JOIN LCK lck ON lck.WarehouseId = ip.WarehouseId
LEFT JOIN dbo.Warehouse w WITH (NOLOCK) ON w.Id = ip.WarehouseId
LEFT JOIN dbo.Suppliers s WITH (NOLOCK) ON s.Id = ip.SupplierId
LEFT JOIN dbo.Bin b WITH (NOLOCK)       ON b.Id = iws.BinId
WHERE ip.ItemId = @ItemMasterId
  AND ip.Qty > 0
ORDER BY 
    WhAvail DESC,
    ip.Qty DESC;";


            foreach (var line in req.Lines)
            {
                var lineRes = new AllocationPreviewLineResult
                {
                    ItemId = line.ItemId,
                    RequestedQty = line.Quantity,
                    AllocatedQty = 0,
                    FullyAllocated = false,
                    Allocations = new List<AllocPiece>()
                };

                var itemMasterId = await conn.ExecuteScalarAsync<int?>(sqlMapItemMaster, new { line.ItemId });
                if (itemMasterId is null || itemMasterId.Value <= 0)
                {
                    result.Lines.Add(lineRes);
                    continue;
                }

                var raw = (await conn.QueryAsync(sqlPreviewCandidates, new { ItemMasterId = itemMasterId.Value }))
                    .Select(r => new
                    {
                        WarehouseId = (int)r.WarehouseId,
                        WarehouseName = (string?)r.WarehouseName,
                        BinId = (int?)r.BinId,
                        BinName = (string?)r.BinName,
                        SupplierId = (int)r.SupplierId,
                        SupplierName = (string?)r.SupplierName,
                        WhAvail = (decimal)r.WhAvail,
                        SupplierQty = (decimal)r.SupplierQty
                    })
                    .ToList();

                var nameMap = raw.ToDictionary(
                    k => (k.WarehouseId, k.SupplierId, k.BinId),
                    v => (v.WarehouseName, v.SupplierName, v.BinName));

                var cands = raw.Select(r =>
                    new AllocCandidate(
                        WarehouseId: r.WarehouseId,
                        BinId: r.BinId,
                        SupplierId: r.SupplierId,
                        WhAvail: r.WhAvail,
                        SupplierQty: r.SupplierQty
                    )).ToList();

                var allocs = MakeAllocation(cands, line.Quantity);
                var allocatedQty = allocs.Sum(a => a.Qty);

                lineRes.AllocatedQty = allocatedQty;
                lineRes.FullyAllocated = allocatedQty >= line.Quantity;

                foreach (var a in allocs)
                {
                    nameMap.TryGetValue((a.WarehouseId, a.SupplierId, a.BinId), out var names);

                    lineRes.Allocations.Add(new AllocPiece
                    {
                        WarehouseId = a.WarehouseId,
                        SupplierId = a.SupplierId,
                        BinId = a.BinId,
                        Qty = a.Qty,
                        WarehouseName = names.WarehouseName,
                        SupplierName = names.SupplierName,
                        BinName = names.BinName
                    });
                }

                result.Lines.Add(lineRes);
            }

            return result;
        }

        // ===================== RUNNING NUMBER =====================
        private async Task<string> GetNextSalesOrderNoAsync(
            IDbConnection conn,
            IDbTransaction tx,
            string prefix = "SO-",
            int width = 4)
        {
            const string sql = @"
DECLARE @n INT;
SELECT @n = ISNULL(MAX(TRY_CONVERT(int, RIGHT(SalesOrderNo, @Width))), 0) + 1
FROM dbo.SalesOrder WITH (UPDLOCK, HOLDLOCK);
SELECT @n;";
            var next = await conn.ExecuteScalarAsync<int>(sql, new { Width = width }, transaction: tx);
            return $"{prefix}{next.ToString().PadLeft(width, '0')}";
        }

        // ===================== UPDATE (no Locked recompute here) =====================
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
 WarehouseId, BinId, Available, SupplierId, LockedQty,
 CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive)
VALUES
(@SalesOrderId, @ItemId, @ItemName, @Uom, @Quantity, @UnitPrice, @Discount, @Tax, @Total,
 @WarehouseId, @BinId, @Available, @SupplierId, @LockedQty,
 @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, 1);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            const string softDeleteMissing = @"
UPDATE dbo.SalesOrderLines
SET IsActive=0, UpdatedBy=@UpdatedBy, UpdatedDate=@UpdatedDate
WHERE SalesOrderId=@SalesOrderId AND IsActive=1
  AND (@KeepIdsCount=0 OR Id NOT IN @KeepIds);";

            const string getItemMasterId = @"
SELECT TOP 1 im.Id
FROM dbo.Item i WITH (NOLOCK)
JOIN dbo.ItemMaster im WITH (NOLOCK) ON im.Sku = i.ItemCode
WHERE i.Id = @ItemId;";

            const string getBinAvail = @"
SELECT TOP 1 iws.BinId, iws.Available
FROM dbo.ItemWarehouseStock iws WITH (NOLOCK)
WHERE iws.ItemId = @ItemMasterId AND iws.WarehouseId = @WarehouseId
ORDER BY iws.Available DESC;";

            const string getSupplier = @"
SELECT TOP 1 ip.SupplierId
FROM dbo.ItemPrice ip WITH (NOLOCK)
WHERE ip.ItemId = @ItemMasterId AND ip.WarehouseId = @WarehouseId
ORDER BY ip.Qty DESC, ip.Id DESC;";

            const string getItemName = @"SELECT TOP 1 ItemName FROM dbo.Item WITH (NOLOCK) WHERE Id = @ItemId;";

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
                        var whId = l.WarehouseId;
                        var itemMasterId = await conn.ExecuteScalarAsync<int?>(getItemMasterId, new { l.ItemId }, tx) ?? 0;

                        int? binId = null;
                        decimal? available = null;
                        int? supplierId = l.SupplierId;

                        if (itemMasterId > 0 && whId.HasValue)
                        {
                            var ba = await conn.QueryFirstOrDefaultAsync<(int? BinId, decimal? Available)>(
                                getBinAvail, new { ItemMasterId = itemMasterId, WarehouseId = whId.Value }, tx);
                            binId = ba.BinId;
                            available = ba.Available;

                            if (!supplierId.HasValue)
                                supplierId = await conn.ExecuteScalarAsync<int?>(getSupplier, new { ItemMasterId = itemMasterId, WarehouseId = whId.Value }, tx);
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
                                LockedQty = (decimal?)null,
                                CreatedBy = (object?)so.UpdatedBy ?? so.CreatedBy,
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

        // ===================== SOFT DELETE =====================
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

        // ===================== QUOTATION → DETAILS (effective available) =====================
        public async Task<QutationDetailsViewInfo?> GetByQuatitonDetails(int id)
        {
            const string sql = @"
SELECT q.Id,q.Number,q.Status,q.CustomerId,c.CustomerName AS CustomerName,
       q.CurrencyId,q.FxRate,q.PaymentTermsId,q.ValidityDate,
       q.Subtotal,q.TaxAmount,q.Rounding,q.GrandTotal,q.NeedsHodApproval,
       cu.CurrencyName,pt.PaymentTermsName,q.ValidityDate,
       COALESCE(cn.GSTPercentage,0) AS GstPct    -- <-- GST here
FROM dbo.Quotation q
LEFT JOIN dbo.Customer     c  ON c.Id = q.CustomerId
LEFT JOIN dbo.Currency     cu ON cu.Id = q.CurrencyId
LEFT JOIN dbo.PaymentTerms pt ON pt.Id = q.PaymentTermsId
LEFT JOIN dbo.Location     ln ON ln.Id = c.LocationId
LEFT JOIN dbo.Country      cn ON cn.Id = ln.CountryId
WHERE q.Id = @Id AND q.IsActive = 1;

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
            /* Effective Available = (OnHand - Reserved) - LockedQtyAcrossActiveSO, clamped to 0 */
            CASE 
              WHEN SUM(W.OnHand - W.Reserved) - ISNULL(Lck.LckQty,0) < 0 
                   THEN 0 
              ELSE SUM(W.OnHand - W.Reserved) - ISNULL(Lck.LckQty,0) 
            END AS Available
        FROM dbo.ItemWarehouseStock W WITH (NOLOCK)
        JOIN dbo.Warehouse wh WITH (NOLOCK) ON wh.Id = W.WarehouseId

        /* Correlated subquery: total LockedQty for this ItemMaster & this Warehouse */
        OUTER APPLY (
            SELECT SUM(sol.LockedQty) AS LckQty
            FROM dbo.SalesOrderLines sol WITH (NOLOCK)
            JOIN dbo.SalesOrder so  WITH (NOLOCK) ON so.Id = sol.SalesOrderId AND so.IsActive = 1
            JOIN dbo.Item i2        WITH (NOLOCK) ON i2.Id = sol.ItemId
            JOIN dbo.ItemMaster im2 WITH (NOLOCK) ON im2.Sku = i2.ItemCode
            WHERE sol.IsActive = 1
              AND im2.Id = IMX.ItemMasterId
              AND sol.WarehouseId = W.WarehouseId
        ) Lck

        WHERE W.ItemId = IMX.ItemMasterId
        GROUP BY W.WarehouseId, wh.Name, ISNULL(Lck.LckQty,0)
        FOR JSON PATH
      ) AS WarehousesJson
    FROM dbo.ItemWarehouseStock W WITH (NOLOCK)
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
