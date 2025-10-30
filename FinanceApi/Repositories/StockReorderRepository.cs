using System.Data;
using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.Data.SqlClient;

namespace FinanceApi.Repositories
{
    public class StockReorderRepository : DynamicRepository, IStockReorderRepository
    {
        private readonly ApplicationDbContext _context;

        public StockReorderRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        // --------------------------------------------------------------------
        // LIST (headers + lines)
        // --------------------------------------------------------------------
        public async Task<IEnumerable<StockReorderDTO>> GetAllAsync()
        {
            const string headersSql = @"
SELECT
    sr.Id,
    sr.WarehouseTypeId,
    w.Name              AS WarehouseName,
    sr.MethodId,
    sr.HorizonDays,
    sr.IncludeLeadTime,
    sr.Status,
    sr.CreatedBy,
    sr.CreatedDate,
    sr.UpdatedBy,
    sr.UpdatedDate,
    sr.IsActive
FROM dbo.StockReorder sr
LEFT JOIN dbo.Warehouse w ON w.Id = sr.WarehouseTypeId
WHERE sr.IsActive = 1
ORDER BY sr.Id;";

            var headers = (await Connection.QueryAsync<StockReorderDTO>(headersSql)).ToList();
            if (headers.Count == 0) return headers;

            var ids = headers.Select(h => h.Id).ToArray();

            const string linesSql = @"
SELECT
    Id,
    StockReorderId,
    WarehouseTypeId,
    ItemId,
    OnHand,
    [Min]          AS Min,      -- bracketed because MIN is a keyword
    [Max]          AS Max,
    ReorderQty,
    LeadDays,
    UsageHorizon,
    Suggested,
    Status,
    Selected,
    CreatedBy,
    CreatedDate,
    UpdatedBy,
    UpdatedDate,
    IsActive
FROM dbo.StockReorderLines
WHERE StockReorderId IN @Ids AND IsActive = 1;";

            var lines = await Connection.QueryAsync<StockReorderLines>(linesSql, new { Ids = ids });

            var byId = headers.ToDictionary(h => h.Id);
            foreach (var ln in lines)
            {
                if (byId.TryGetValue(ln.StockReorderId, out var parent))
                {
                    parent.LineItems ??= new List<StockReorderLines>();
                    parent.LineItems.Add(ln);
                }
            }

            return headers;
        }

        // --------------------------------------------------------------------
        // WAREHOUSE ITEMS (source rows for planning grid)
        // --------------------------------------------------------------------
        public async Task<IEnumerable<StockReorderWarehouseItems>> GetWarehouseItemsAsync(long warehouseId)
        {
            const string sql = @"
SELECT
    -- Item level
    im.Id                     AS ItemId,
    im.Name                   AS ItemName,
    im.Sku                    AS Sku,
    iws.WarehouseId           AS WarehouseId,
    w.Name                    AS WarehouseName,
    ISNULL(iws.OnHand, 0)     AS OnHand,
    ISNULL(iws.Reserved, 0)   AS Reserved,
    ISNULL(iws.MinQty, 0)     AS MinQty,
    iws.ReorderQty            AS ReorderQty,
    iws.MaxQty                AS MaxQty,
    iws.LeadTimeDays          AS LeadDays,
    CAST(0 AS DECIMAL(18,3))  AS UsageHorizon,
    ISNULL(iws.MinQty, 0)     AS SafetyStock,

    -- Supplier level (can be multiple rows per item)
    sp.Id                     AS SupplierId,
    sp.Name                   AS SupplierName,
    ISNULL(ip.Price, 0)       AS Price,
    ISNULL(ip.Qty, 0)         AS Qty
FROM dbo.ItemWarehouseStock iws
JOIN dbo.ItemMaster  im ON im.Id = iws.ItemId
JOIN dbo.Warehouse   w  ON w.Id  = iws.WarehouseId
LEFT JOIN dbo.ItemPrice ip ON ip.ItemId = im.Id              -- your price list
LEFT JOIN dbo.Suppliers  sp ON sp.Id     = ip.SupplierId
WHERE iws.WarehouseId = @WarehouseId
ORDER BY im.Sku, im.Name, sp.Name;";

            var args = new { WarehouseId = warehouseId };

            if (Connection.State != ConnectionState.Open)
                await (Connection as SqlConnection)!.OpenAsync();

            // Multi-mapping: item columns then SupplierId marks split
            var map = new Dictionary<long, StockReorderWarehouseItems>();

            var result = await Connection.QueryAsync<StockReorderWarehouseItems, StockReorderSupplier, StockReorderWarehouseItems>(
                sql,
                (item, sup) =>
                {
                    if (!map.TryGetValue(item.ItemId, out var existing))
                    {
                        existing = item;
                        existing.Suppliers = new List<StockReorderSupplier>();
                        map[item.ItemId] = existing;
                    }

                    // Only add supplier row if present (SupplierId not null/0). Qty defaults to 0 (user will input)
                    if (sup != null && sup.SupplierId > 0)
                    {
                        
                        existing.Suppliers.Add(sup);
                    }
                    return existing;
                },
                args,
                splitOn: "SupplierId"
            );

            // De-dup consolidated list
            return map.Values;
        }



        // --------------------------------------------------------------------
        // GET BY ID (header + lines)
        // --------------------------------------------------------------------
        public async Task<StockReorderDTO?> GetByIdAsync(int id)
        {
            const string headerSql = @"
SELECT TOP (1)
    sr.Id,
    sr.WarehouseTypeId,
    w.Name              AS WarehouseName,
    sr.MethodId,
    sr.HorizonDays,
    sr.IncludeLeadTime,
    sr.Status,
    sr.CreatedBy,
    sr.CreatedDate,
    sr.UpdatedBy,
    sr.UpdatedDate,
    sr.IsActive
FROM dbo.StockReorder sr
LEFT JOIN dbo.Warehouse w ON w.Id = sr.WarehouseTypeId
WHERE sr.Id = @Id AND sr.IsActive = 1;";

            var header = await Connection.QueryFirstOrDefaultAsync<StockReorderDTO>(headerSql, new { Id = id });
            if (header == null) return null;

            const string linesSql = @"
SELECT
    Id,
    StockReorderId,
    WarehouseTypeId,
    ItemId,
    OnHand,
    [Min]       AS Min,
    [Max]       AS Max,
    ReorderQty,
    LeadDays,
    UsageHorizon,
    Suggested,
    Status,
    Selected,
    CreatedBy,
    CreatedDate,
    UpdatedBy,
    UpdatedDate,
    IsActive
FROM dbo.StockReorderLines
WHERE StockReorderId = @Id AND IsActive = 1
ORDER BY Id;";

            var lines = await Connection.QueryAsync<StockReorderLines>(linesSql, new { Id = id });
            header.LineItems = lines.ToList();

            return header;
        }

        // --------------------------------------------------------------------
        // CREATE (header + lines)
        // --------------------------------------------------------------------
        public async Task<int> CreateAsync(StockReorder stockReorder)
        {
            if (stockReorder is null) throw new ArgumentNullException(nameof(stockReorder));

            var now = DateTime.UtcNow;
            if (stockReorder.CreatedDate == default) stockReorder.CreatedDate = now;
            if (stockReorder.UpdatedDate == default) stockReorder.UpdatedDate = now;

            const string insertHeaderSql = @"
INSERT INTO dbo.StockReorder
(
    WarehouseTypeId, MethodId, HorizonDays, IncludeLeadTime,
    Status, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
)
OUTPUT INSERTED.Id
VALUES
(
    @WarehouseTypeId, @MethodId, @HorizonDays, @IncludeLeadTime,
    @Status, @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, @IsActive
);";

            const string insertLineSql = @"
INSERT INTO dbo.StockReorderLines
(
    StockReorderId, WarehouseTypeId, ItemId,
    OnHand, [Min], [Max],ReorderQty, LeadDays, UsageHorizon, Suggested,
    Status, Selected, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
)
VALUES
(
    @StockReorderId, @WarehouseTypeId, @ItemId,
    @OnHand, @Min, @Max,@ReorderQty, @LeadDays, @UsageHorizon, @Suggested,
    @Status, @Selected, @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, 1
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
                        stockReorder.WarehouseTypeId,
                        stockReorder.MethodId,
                        stockReorder.HorizonDays,
                        stockReorder.IncludeLeadTime,
                        stockReorder.Status,
                        stockReorder.CreatedBy,
                        stockReorder.CreatedDate,
                        stockReorder.UpdatedBy,
                        stockReorder.UpdatedDate,
                        IsActive = stockReorder.IsActive
                    },
                    tx);

                if (stockReorder.LineItems?.Count > 0)
                {
                    var lineParams = stockReorder.LineItems.Select(l => new
                    {
                        StockReorderId = newId,
                        l.WarehouseTypeId,
                        l.ItemId,
                        l.OnHand,
                        Min = l.Min,
                        Max = l.Max,
                        l.ReorderQty,
                        l.LeadDays,
                        l.UsageHorizon,
                        l.Suggested,
                        l.Status,
                        l.Selected,
                        CreatedBy = stockReorder.CreatedBy,
                        CreatedDate = stockReorder.CreatedDate,
                        UpdatedBy = stockReorder.UpdatedBy,
                        UpdatedDate = stockReorder.UpdatedDate
                    });

                    await conn.ExecuteAsync(insertLineSql, lineParams, tx);
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

        // --------------------------------------------------------------------
        // UPDATE (header + upsert lines + soft delete missing)
        // --------------------------------------------------------------------
        public async Task UpdateAsync(StockReorder stockReorder)
        {
            if (stockReorder is null) throw new ArgumentNullException(nameof(stockReorder));

            var now = DateTime.UtcNow;
            stockReorder.UpdatedDate = now;

            const string updateHeaderSql = @"
UPDATE dbo.StockReorder
SET
    WarehouseTypeId = @WarehouseTypeId,
    MethodId        = @MethodId,
    HorizonDays     = @HorizonDays,
    IncludeLeadTime = @IncludeLeadTime,
    Status          = @Status,
    UpdatedBy       = @UpdatedBy,
    UpdatedDate     = @UpdatedDate
WHERE Id = @Id;";

            const string updateLineSql = @"
UPDATE dbo.StockReorderLines
SET
    WarehouseTypeId = @WarehouseTypeId,
    ItemId          = @ItemId,
    OnHand          = @OnHand,
    [Min]           = @Min,
    [Max]           = @Max,
    ReorderQty = @ReorderQty,
    LeadDays        = @LeadDays,
    UsageHorizon    = @UsageHorizon,
    Suggested       = @Suggested,
    Status          = @Status,
    Selected        = @Selected,
    UpdatedBy       = @UpdatedBy,
    UpdatedDate     = @UpdatedDate,
    IsActive        = 1
WHERE Id = @Id AND StockReorderId = @StockReorderId;";

            const string insertLineSql = @"
INSERT INTO dbo.StockReorderLines
(
    StockReorderId, WarehouseTypeId, ItemId,
    OnHand, [Min], [Max],ReorderQty, LeadDays, UsageHorizon, Suggested,
    Status, Selected, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
)
VALUES
(
    @StockReorderId, @WarehouseTypeId, @ItemId,
    @OnHand, @Min, @Max,@ReorderQty, @LeadDays, @UsageHorizon, @Suggested,
    @Status, @Selected, @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, 1
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            const string softDeleteMissingSql = @"
UPDATE dbo.StockReorderLines
SET IsActive = 0, UpdatedBy = @UpdatedBy, UpdatedDate = @UpdatedDate
WHERE StockReorderId = @StockReorderId
  AND IsActive = 1
  AND (@KeepIdsCount = 0 OR Id NOT IN @KeepIds);";

            var conn = Connection;
            if (conn.State != ConnectionState.Open)
                await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                // Header
                await conn.ExecuteAsync(updateHeaderSql, new
                {
                    stockReorder.WarehouseTypeId,
                    stockReorder.MethodId,
                    stockReorder.HorizonDays,
                    stockReorder.IncludeLeadTime,
                    stockReorder.Status,
                    stockReorder.UpdatedBy,
                    stockReorder.UpdatedDate,
                    stockReorder.Id
                }, tx);

                // Upsert lines
                var keepIds = new List<int>();

                if (stockReorder.LineItems?.Count > 0)
                {
                    foreach (var l in stockReorder.LineItems)
                    {
                        if (l.Id > 0)
                        {
                            await conn.ExecuteAsync(updateLineSql, new
                            {
                                l.Id,
                                StockReorderId = stockReorder.Id,
                                l.WarehouseTypeId,
                                l.ItemId,
                                l.OnHand,
                                Min = l.Min,
                                Max = l.Max,
                                l.ReorderQty,
                                l.LeadDays,
                                l.UsageHorizon,
                                l.Suggested,
                                l.Status,
                                l.Selected,
                                UpdatedBy = stockReorder.UpdatedBy,
                                UpdatedDate = now
                            }, tx);

                            keepIds.Add(l.Id);
                        }
                        else
                        {
                            var newLineId = await conn.ExecuteScalarAsync<int>(insertLineSql, new
                            {
                                StockReorderId = stockReorder.Id,
                                l.WarehouseTypeId,
                                l.ItemId,
                                l.OnHand,
                                Min = l.Min,
                                Max = l.Max,
                                l.ReorderQty,
                                l.LeadDays,
                                l.UsageHorizon,
                                l.Suggested,
                                l.Status,
                                l.Selected,
                                CreatedBy = stockReorder.UpdatedBy ?? stockReorder.CreatedBy,
                                CreatedDate = now,
                                UpdatedBy = stockReorder.UpdatedBy,
                                UpdatedDate = now
                            }, tx);

                            keepIds.Add(newLineId);
                        }
                    }
                }

                // Soft delete removed lines
                var keepIdsParam = keepIds.Count == 0 ? new[] { -1 } : keepIds.ToArray();

                await conn.ExecuteAsync(softDeleteMissingSql, new
                {
                    StockReorderId = stockReorder.Id,
                    KeepIds = keepIdsParam,
                    KeepIdsCount = keepIds.Count,
                    UpdatedBy = stockReorder.UpdatedBy,
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

        // --------------------------------------------------------------------
        // DEACTIVATE (header + lines)
        // --------------------------------------------------------------------
        public async Task DeactivateAsync(int id, int updatedBy)
        {
            const string sqlHeader = @"
UPDATE dbo.StockReorder
SET IsActive = 0, UpdatedBy = @UpdatedBy, UpdatedDate = SYSUTCDATETIME()
WHERE Id = @Id;";

            const string sqlLines = @"
UPDATE dbo.StockReorderLines
SET IsActive = 0, UpdatedBy = @UpdatedBy, UpdatedDate = SYSUTCDATETIME()
WHERE StockReorderId = @Id AND IsActive = 1;";

            var conn = Connection;
            if (conn.State != ConnectionState.Open)
                await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                var affectedHeader = await conn.ExecuteAsync(sqlHeader, new { Id = id, UpdatedBy = updatedBy }, tx);
                if (affectedHeader == 0)
                    throw new KeyNotFoundException("StockReorder not found.");

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
