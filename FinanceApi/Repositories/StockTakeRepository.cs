using System.Data;
using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.Data.SqlClient;

namespace FinanceApi.Repositories
{
    public class StockTakeRepository : DynamicRepository, IStockTakeRepository
    {
        private readonly ApplicationDbContext _context;

        public StockTakeRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
        {

        }

        public async Task<IEnumerable<StockTakeDTO>> GetAllAsync()
        {
            const string headersSql = @"
        SELECT
            st.Id,
            st.TakeTypeId,
            st.WarehouseTypeId,
            ISNULL(w.Name,'') AS WarehouseName,
            st.LocationId,
            ISNULL(B.BinName,'') AS LocationName,
            st.StrategyId,
            ISNULL(s.StrategyName,'') AS StrategyName,
            st.SupplierId,
            ISNULL(sp.Name,'') AS SupplierName,
            st.Freeze,
            st.Status,
            st.CreatedBy,
            st.CreatedDate,
            st.UpdatedBy,
            st.UpdatedDate,
            st.IsActive
        FROM StockTake st
        LEFT JOIN Warehouse w ON st.WarehouseTypeId = w.Id
        LEFT JOIN BIN  B ON st.LocationId      = B.Id
        LEFT JOIN Strategy  s ON st.StrategyId      = s.Id
        LEFT JOIN Suppliers  sp ON st.SupplierId      = sp.Id
        WHERE st.IsActive = 1
        ORDER BY st.Id;";

            // 1) Load headers
            var headers = (await Connection.QueryAsync<StockTakeDTO>(headersSql)).ToList();
            if (headers.Count == 0) return headers;

            // 2) Load all lines for these headers
            var ids = headers.Select(h => h.Id).ToArray();
            const string linesSql = @"
        SELECT
            Id,
            StockTakeId,
            ItemId,
            OnHand,
            CountedQty,
            BadCountedQty,
            VarianceQty,
            Barcode,
            Reason,
            Remarks,
            Selected,
            CreatedBy,
            CreatedDate,
            UpdatedBy,
            UpdatedDate,
            IsActive
        FROM StockTakeLines
        WHERE StockTakeId IN @Ids AND IsActive = 1;";

            var lines = await Connection.QueryAsync<StockTakeLines>(linesSql, new { Ids = ids });

            // 3) Attach
            var byId = headers.ToDictionary(h => h.Id);
            foreach (var ln in lines)
            {
                if (byId.TryGetValue(ln.StockTakeId, out var parent))
                    parent.LineItems.Add(ln);
            }

            return headers;
        }


        public async Task<IEnumerable<StockTakeWarehouseItem>> GetWarehouseItemsAsync(
     long warehouseId,
     long supplierId,
     long binId,
     byte takeTypeId,
     long? strategyId
 )
        {
            const string sql = @"
SELECT
    im.Id   AS ItemId,
    im.Sku  AS Sku,
    im.Name AS ItemName,
    iws.WarehouseId,
    iws.BinId,
    iws.OnHand,
    iws.Reserved,
    (iws.OnHand - iws.Reserved) AS AvailableQty,
    iws.MinQty,
    iws.MaxQty,
    iws.ReorderQty,
    @SupplierId AS SupplierId
    -- If you also want to *show* a price, uncomment this:
    -- ,(
    --   SELECT TOP 1 ip.Price
    --   FROM dbo.ItemPrice ip
    --   WHERE ip.ItemId = im.Id
    --     AND (@SupplierId IS NULL OR ip.SupplierId = @SupplierId)
    --   ORDER BY ip.UpdatedDate DESC, ip.Id DESC
    -- ) AS Price
FROM dbo.ItemWarehouseStock iws
JOIN dbo.ItemMaster im ON im.Id = iws.ItemId
WHERE
    iws.WarehouseId = @WarehouseId
    AND (@BinId IS NULL OR iws.BinId = @BinId)
    AND (
         @TakeTypeId = 1
         OR (@TakeTypeId = 2 AND iws.StrategyId = @StrategyId)
    )
    AND (
        @SupplierId IS NULL
        OR EXISTS (
            SELECT 1
            FROM dbo.ItemPrice ip
            WHERE ip.ItemId = im.Id
              AND ip.SupplierId = @SupplierId
        )
    )
ORDER BY im.Sku, im.Name;
";

            long? binParam = binId; // pass NULL from controller if you support "All bins"

            return await Connection.QueryAsync<StockTakeWarehouseItem>(
                sql,
                new
                {
                    WarehouseId = warehouseId,
                    BinId = binParam,
                    TakeTypeId = takeTypeId,
                    StrategyId = strategyId,
                    SupplierId = supplierId
                });
        }



        public async Task<StockTakeDTO?> GetByIdAsync(int id)
        {
            const string headerSql = @"
        SELECT TOP (1)
            st.Id,
            st.TakeTypeId,
            st.WarehouseTypeId,
            ISNULL(w.Name,'') AS WarehouseName,
            st.LocationId,
            ISNULL(B.BinName,'') AS LocationName,
            st.StrategyId,
            ISNULL(s.StrategyName,'') AS StrategyName,
            st.SupplierId,
            ISNULL(sp.Name,'') AS SupplierName,
            st.Freeze,
            st.Status,
            st.CreatedBy,
            st.CreatedDate,
            st.UpdatedBy,
            st.UpdatedDate,
            st.IsActive
        FROM StockTake st
        LEFT JOIN Warehouse w ON st.WarehouseTypeId = w.Id
        LEFT JOIN BIN  B ON st.LocationId      = B.Id
        LEFT JOIN Strategy  s ON st.StrategyId      = s.Id
        LEFT JOIN Suppliers  sp ON st.SupplierId      = sp.Id
        WHERE st.Id = @Id AND st.IsActive = 1;";

            // 1) header
            var header = await Connection.QueryFirstOrDefaultAsync<StockTakeDTO>(headerSql, new { Id = id });
            if (header == null) return null;

            const string linesSql = @"
        SELECT
            Id,
            StockTakeId,
            ItemId,
            OnHand,
            CountedQty,
            BadCountedQty,
            VarianceQty,
            Barcode,
            Reason,
            Remarks,
            Selected,
            CreatedBy,
            CreatedDate,
            UpdatedBy,
            UpdatedDate,
            IsActive
        FROM StockTakeLines
        WHERE StockTakeId = @Id AND IsActive = 1
        ORDER BY Id;";

            // 2) lines
            var lines = await Connection.QueryAsync<StockTakeLines>(linesSql, new { Id = id });
            header.LineItems = lines.ToList();

            return header;
        }



        public async Task<int> CreateAsync(StockTake stockTake)
        {
            if (stockTake is null) throw new ArgumentNullException(nameof(stockTake));

            var now = DateTime.UtcNow;
            if (stockTake.CreatedDate == default) stockTake.CreatedDate = now;
            if (stockTake.UpdatedDate == default) stockTake.UpdatedDate = now;

            const string insertHeaderSql = @"
INSERT INTO StockTake
(
    WarehouseTypeId, LocationId, TakeTypeId, StrategyId,SupplierId,
    Freeze, Status, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
)
OUTPUT INSERTED.Id
VALUES
(
    @WarehouseTypeId, @LocationId, @TakeTypeId, @StrategyId,@SupplierId,
    @Freeze, @Status, @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, @IsActive
);";

            const string insertLinesSql = @"
INSERT INTO StockTakeLines
(
    StockTakeId, ItemId, OnHand, CountedQty,BadCountedQty, VarianceQty,Reason,
    Barcode, Remarks,Selected, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
)
VALUES
(
    @StockTakeId, @ItemId, @OnHand, @CountedQty,@BadCountedQty, @VarianceQty,@Reason,
    @Barcode, @Remarks,@Selected, @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, @IsActive
);";

            // ✅ HOLD the same connection instance for the whole method
            var conn = Connection;                             // capture once
            if (conn.State != ConnectionState.Open)
                await (conn as SqlConnection)!.OpenAsync();    // or conn.Open() if you prefer sync

            using var tx = conn.BeginTransaction();            // ✅ same instance

            try
            {
                // 1) Insert header
                var newId = await conn.ExecuteScalarAsync<int>(
                    insertHeaderSql,
                    new
                    {
                        stockTake.WarehouseTypeId,
                        stockTake.LocationId,
                        stockTake.TakeTypeId,
                        stockTake.StrategyId,
                        stockTake.SupplierId,
                        stockTake.Freeze,
                        stockTake.Status,
                        stockTake.CreatedBy,
                        stockTake.CreatedDate,
                        stockTake.UpdatedBy,
                        stockTake.UpdatedDate,
                        IsActive = stockTake.IsActive
                    },
                    transaction: tx
                );

                // 2) Insert lines (if any)
                if (stockTake.LineItems?.Count > 0)
                {
                    var lineParams = stockTake.LineItems.Select(l => new
                    {
                        StockTakeId = newId,
                        l.ItemId,
                        l.OnHand,
                        CountedQty = l.CountedQty,
                        BadCountedQty = l.BadCountedQty,
                        VarianceQty = (l.CountedQty.HasValue || l.BadCountedQty.HasValue)
    ? ((l.CountedQty ?? 0m) + (l.BadCountedQty ?? 0m)) - l.OnHand
    : (decimal?)null,
                        l.Reason,
                        l.Barcode,
                        l.Remarks,
                        l.Selected,
                        CreatedBy = stockTake.CreatedBy,
                        CreatedDate = stockTake.CreatedDate,
                        UpdatedBy = stockTake.UpdatedBy,
                        UpdatedDate = stockTake.UpdatedDate,
                        IsActive = true
                    });

                    await conn.ExecuteAsync(insertLinesSql, lineParams, transaction: tx);
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




        public async Task UpdateAsync(StockTake updatedStockTake)
        {
            if (updatedStockTake is null) throw new ArgumentNullException(nameof(updatedStockTake));

            var now = DateTime.UtcNow;
            updatedStockTake.UpdatedDate = now;

            const string updateHeaderSql = @"
UPDATE StockTake
SET
    WarehouseTypeId = @WarehouseTypeId,
    LocationId      = @LocationId,
    TakeTypeId      = @TakeTypeId,   -- if you renamed: TakeType
    StrategyId      = @StrategyId,
    SupplierId      = @SupplierId,
    Freeze          = @Freeze,
    Status          = @Status,
    UpdatedBy       = @UpdatedBy,
    UpdatedDate     = @UpdatedDate
WHERE Id = @Id;";

            const string updateLineSql = @"
UPDATE StockTakeLines
SET
    ItemId      = @ItemId,
    OnHand      = @OnHand,
    CountedQty  = @CountedQty,
    BadCountedQty  = @BadCountedQty,
    VarianceQty = @VarianceQty,
    Reason = @Reason,
    Barcode     = @Barcode,
    Remarks     = @Remarks,
    Selected = @Selected,
    UpdatedBy   = @UpdatedBy,
    UpdatedDate = @UpdatedDate,
    IsActive    = 1
WHERE Id = @Id AND StockTakeId = @StockTakeId;";

            const string insertLineSql = @"
INSERT INTO StockTakeLines
(
    StockTakeId, ItemId, OnHand, CountedQty, BadCountedQty,VarianceQty,Reason
    Barcode, Remarks,Selected, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
)
VALUES
(
    @StockTakeId, @ItemId, @OnHand, @CountedQty, @BadCountedQty,@VarianceQty,@Reason
    @Barcode, @Remarks,@Selected, @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, 1
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            const string softDeleteMissingSql = @"
UPDATE StockTakeLines
SET IsActive = 0, UpdatedBy = @UpdatedBy, UpdatedDate = @UpdatedDate
WHERE StockTakeId = @StockTakeId
  AND IsActive = 1
  AND (@KeepIdsCount = 0 OR Id NOT IN @KeepIds);";

            // ✅ Capture the SAME connection instance and open it
            var conn = Connection;
            if (conn.State != ConnectionState.Open)
                await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                // 1) Update header
                await conn.ExecuteAsync(updateHeaderSql, new
                {
                    updatedStockTake.WarehouseTypeId,
                    updatedStockTake.LocationId,
                    updatedStockTake.TakeTypeId,   // if renamed -> TakeType
                    updatedStockTake.StrategyId,
                    updatedStockTake.SupplierId,
                    updatedStockTake.Freeze,
                    updatedStockTake.Status,
                    updatedStockTake.UpdatedBy,
                    updatedStockTake.UpdatedDate,
                    updatedStockTake.Id
                }, tx);

                // 2) Upsert lines
                var keepIds = new List<int>();

                if (updatedStockTake.LineItems?.Count > 0)
                {
                    foreach (var l in updatedStockTake.LineItems)
                    {
                        decimal? variance =
    (l.CountedQty.HasValue || l.BadCountedQty.HasValue)
        ? ((l.CountedQty ?? 0m) + (l.BadCountedQty ?? 0m)) - l.OnHand
        : (decimal?)null;

                        if (l.Id > 0)
                        {
                            await conn.ExecuteAsync(updateLineSql, new
                            {
                                Id = l.Id,
                                StockTakeId = updatedStockTake.Id,
                                l.ItemId,
                                l.OnHand,
                                CountedQty = l.CountedQty,
                                BadCountedQty = l.BadCountedQty,
                                VarianceQty = variance,
                                l.Reason,
                                l.Barcode,
                                l.Remarks,
                                l.Selected,
                                UpdatedBy = updatedStockTake.UpdatedBy,
                                UpdatedDate = now
                            }, tx);

                            keepIds.Add(l.Id);
                        }
                        else
                        {
                            var newLineId = await conn.ExecuteScalarAsync<int>(insertLineSql, new
                            {
                                StockTakeId = updatedStockTake.Id,
                                l.ItemId,
                                l.OnHand,
                                CountedQty = l.CountedQty,
                                BadCountedQty = l.BadCountedQty,
                                VarianceQty = variance,
                                l.Reason,
                                l.Barcode,
                                l.Remarks,
                                l.Selected,
                                CreatedBy = updatedStockTake.UpdatedBy ?? updatedStockTake.CreatedBy,
                                CreatedDate = now,
                                UpdatedBy = updatedStockTake.UpdatedBy,
                                UpdatedDate = now
                            }, tx);

                            keepIds.Add(newLineId);
                        }
                    }
                }

                // 3) Soft delete lines not sent from UI
                var keepIdsParam = keepIds.Count == 0 ? new[] { -1 } : keepIds.ToArray();

                await conn.ExecuteAsync(softDeleteMissingSql, new
                {
                    StockTakeId = updatedStockTake.Id,
                    KeepIds = keepIdsParam,       // Dapper expands for IN (@KeepIds)
                    KeepIdsCount = keepIds.Count,
                    UpdatedBy = updatedStockTake.UpdatedBy,
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
UPDATE StockTake
SET IsActive = 0, UpdatedBy = @UpdatedBy, UpdatedDate = SYSUTCDATETIME()
WHERE Id = @Id;";

            const string sqlLines = @"
UPDATE StockTakeLines
SET IsActive = 0, UpdatedBy = @UpdatedBy, UpdatedDate = SYSUTCDATETIME()
WHERE StockTakeId = @Id AND IsActive = 1;";

            // capture the same connection instance
            var conn = Connection;
            if (conn.State != ConnectionState.Open)
                await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                // optional: block if already posted/final
                // var status = await conn.ExecuteScalarAsync<int>("SELECT Status FROM StockTake WHERE Id=@Id", new { Id = id }, tx);
                // if (status == (int)StockTakeStatus.Posted) throw new InvalidOperationException("Cannot deactivate a posted stock take.");

                var affectedHeader = await conn.ExecuteAsync(sqlHeader, new { Id = id, UpdatedBy = updatedBy }, tx);
                if (affectedHeader == 0)
                    throw new KeyNotFoundException("StockTake not found.");

                await conn.ExecuteAsync(sqlLines, new { Id = id, UpdatedBy = updatedBy }, tx);

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // In StockTakeRepository
        public async Task<int> CreateFromStockTakeAsync(
            int stockTakeId,
            string? reason,
            string? remarks,
            string userName,
            bool applyToStock = true,
            bool markPosted = true,
            DateTime? txnDateOverride = null,
            bool onlySelected = true)
        {
            var conn = Connection;
            if (conn.State != ConnectionState.Open)
                await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();
            try
            {
                // 1) Header
                const string headerSql = @"
SELECT TOP(1)
    Id, WarehouseTypeId, LocationId, TakeTypeId, StrategyId,SupplierId, Freeze, Status, IsActive
FROM StockTake
WHERE Id = @Id AND IsActive = 1;";
                var header = await conn.QueryFirstOrDefaultAsync(headerSql, new { Id = stockTakeId }, tx);
                if (header is null) throw new KeyNotFoundException($"StockTake {stockTakeId} not found.");

                // Guard: only Approved can post; Posted is blocked
                int status = (int)header.Status;
                if (status == (int)StockTakeStatus.Posted)
                    throw new InvalidOperationException("This stock take is already posted.");
                if (status != (int)StockTakeStatus.Approved)
                    throw new InvalidOperationException("Only Approved stock takes can be posted.");

                // 2) Lines (optionally only Selected)
                var linesSql = @"
SELECT Id, StockTakeId, ItemId, OnHand, CountedQty, BadCountedQty, VarianceQty,
       Barcode, Remarks, Reason, Selected, IsActive
FROM StockTakeLines
WHERE StockTakeId = @Id AND IsActive = 1";
                if (onlySelected) linesSql += " AND Selected = 1;";
                else linesSql += ";";

                var lines = (await conn.QueryAsync<StockTakeLines>(linesSql, new { Id = stockTakeId }, tx)).ToList();

                if (lines.Count == 0)
                {
                    if (markPosted)
                        await conn.ExecuteAsync(
                            "UPDATE StockTake SET Status=@S, UpdatedBy=@U, UpdatedDate=SYSUTCDATETIME() WHERE Id=@Id;",
                            new { S = (int)StockTakeStatus.Posted, U = userName, Id = stockTakeId }, tx);
                    tx.Commit();
                    return 0;
                }

                var now = DateTime.UtcNow;
                var txnDate = txnDateOverride ?? now;

                // 3) Insert StockTakeInventoryAdjustment rows (variance-only)
                const string insertAdjSql = @"
INSERT INTO StockTakeInventoryAdjustment
(
    ItemId, WarehouseTypeId, LocationId,SupplierId,
    TxnDate, Reason, Remarks,
    SourceType, SourceId, SourceLineId,
    QtyIn, QtyOut,
    QtyBefore, QtyAfter,
    CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
)
VALUES
(
    @ItemId, @WarehouseTypeId, @LocationId,@SupplierId,
    @TxnDate, @Reason, @Remarks,
    @SourceType, @SourceId, @SourceLineId,
    @QtyIn, @QtyOut,
    @QtyBefore, @QtyAfter,
    @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, 1
);";

                var rows = new List<object>();

                foreach (var l in lines)
                {
                    decimal before = l.OnHand;
                    decimal good = l.CountedQty ?? 0m;
                    decimal bad = l.BadCountedQty ?? 0m;
                    decimal? variance = l.VarianceQty ?? (good + bad - before);

                    if (!variance.HasValue || variance.Value == 0m) continue;

                    decimal qtyIn = variance.Value > 0 ? variance.Value : 0m;
                    decimal qtyOut = variance.Value < 0 ? -variance.Value : 0m;

                    rows.Add(new
                    {
                        ItemId = l.ItemId,
                        WarehouseTypeId = (int)header.WarehouseTypeId,
                        LocationId = (int)header.LocationId,
                        SupplierId = (int)header.SupplierId,
                        TxnDate = txnDate,
                        // prefer line reason; fall back to request reason
                        Reason = l.Reason,
                        Remarks = (l.Remarks?.Trim().Length > 0 ? l.Remarks : remarks) ?? null,
                        SourceType = "StockTake",
                        SourceId = stockTakeId,
                        SourceLineId = l.Id,
                        QtyIn = qtyIn,
                        QtyOut = qtyOut,
                        QtyBefore = before,
                        // QtyAfter = total physical counted (good+bad). If you want only good, change here.
                        QtyAfter = (decimal?)(good + bad),
                        CreatedBy = userName,
                        CreatedDate = now,
                        UpdatedBy = userName,
                        UpdatedDate = now
                    });
                }

                if (rows.Count == 0)
                {
                    if (markPosted)
                        await conn.ExecuteAsync(
                            "UPDATE StockTake SET Status=@S, UpdatedBy=@U, UpdatedDate=SYSUTCDATETIME() WHERE Id=@Id;",
                            new { S = (int)StockTakeStatus.Posted, U = userName, Id = stockTakeId }, tx);
                    tx.Commit();
                    return 0;
                }

                await conn.ExecuteAsync(insertAdjSql, rows, tx);

                // 4) Apply to stock (set OnHand = good + bad)
                // 4) Apply to stock (reset OnHand to total physical = usable + unusable)
                // 4) Apply to stock (reset OnHand to total physical = usable + unusable)
                if (applyToStock)
                {
                    const string upsertStockSql = @"
MERGE dbo.ItemWarehouseStock AS tgt
USING (VALUES (@ItemId, @WarehouseId, @BinId, @TotalPhysical))
      AS src (ItemId, WarehouseId, BinId, TotalPhysical)
ON (tgt.ItemId = src.ItemId
    AND tgt.WarehouseId = src.WarehouseId
    AND tgt.BinId = src.BinId)
WHEN MATCHED THEN
    UPDATE SET OnHand = src.TotalPhysical
WHEN NOT MATCHED THEN
    INSERT (ItemId, WarehouseId, BinId, OnHand, Reserved, MinQty, MaxQty, ReorderQty)
    VALUES (src.ItemId, src.WarehouseId, src.BinId, src.TotalPhysical, 0, 0, 0, 0);";

                    // Tip: batch execute for performance
                    var upsertRows = lines.Select(l => new {
                        ItemId = l.ItemId,
                        WarehouseId = (int)header.WarehouseTypeId, // map to your actual WarehouseId
                        BinId = (int)header.LocationId,      // map to your actual BinId
                        TotalPhysical = (l.CountedQty ?? 0m) + (l.BadCountedQty ?? 0m)
                    }).ToList();

                    await conn.ExecuteAsync(upsertStockSql, upsertRows, tx);
                }



                // 5) Mark posted
                if (markPosted)
                {
                    await conn.ExecuteAsync(
                        "UPDATE StockTake SET Status=@S, UpdatedBy=@U, UpdatedDate=@D WHERE Id=@Id;",
                        new { S = (int)StockTakeStatus.Posted, U = userName, D = now, Id = stockTakeId }, tx);
                }

                tx.Commit();
                return rows.Count;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }


    }
}
