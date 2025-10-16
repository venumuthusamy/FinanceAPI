using System.Data;
using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

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
            ISNULL(l.Name,'') AS LocationName,
            st.StrategyId,
            ISNULL(s.StrategyName,'') AS StrategyName,
            st.Freeze,
            st.Status,
            st.CreatedBy,
            st.CreatedDate,
            st.UpdatedBy,
            st.UpdatedDate,
            st.IsActive
        FROM StockTake st
        LEFT JOIN Warehouse w ON st.WarehouseTypeId = w.Id
        LEFT JOIN Location  l ON st.LocationId      = l.Id
        LEFT JOIN Strategy  s ON st.StrategyId      = s.Id
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
            AvailableQty,
            CountedQty,
            VarianceQty,
            Barcode,
            Remarks,
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



        public async Task<StockTakeDTO?> GetByIdAsync(int id)
        {
            const string headerSql = @"
        SELECT TOP (1)
            st.Id,
            st.TakeTypeId,
            st.WarehouseTypeId,
            ISNULL(w.Name,'') AS WarehouseName,
            st.LocationId,
            ISNULL(l.Name,'') AS LocationName,
            st.StrategyId,
            ISNULL(s.StrategyName,'') AS StrategyName,
            st.Freeze,
            st.Status,
            st.CreatedBy,
            st.CreatedDate,
            st.UpdatedBy,
            st.UpdatedDate,
            st.IsActive
        FROM StockTake st
        LEFT JOIN Warehouse w ON st.WarehouseTypeId = w.Id
        LEFT JOIN Location  l ON st.LocationId      = l.Id
        LEFT JOIN Strategy  s ON st.StrategyId      = s.Id
        WHERE st.Id = @Id AND st.IsActive = 1;";

            // 1) header
            var header = await Connection.QueryFirstOrDefaultAsync<StockTakeDTO>(headerSql, new { Id = id });
            if (header == null) return null;

            const string linesSql = @"
        SELECT
            Id,
            StockTakeId,
            ItemId,
            AvailableQty,
            CountedQty,
            VarianceQty,
            Barcode,
            Remarks,
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

            // Timestamps (use UTC to be consistent; DB has defaults, but we pass explicitly)
            var now = DateTime.UtcNow;
            if (stockTake.CreatedDate == default) stockTake.CreatedDate = now;
            if (stockTake.UpdatedDate == default) stockTake.UpdatedDate = now;

            // IMPORTANT: Your table does NOT have PoLines. Removed.
            const string insertHeaderSql = @"
INSERT INTO StockTake
(
    WarehouseTypeId, LocationId, TakeTypeId, StrategyId,
    Freeze, Status, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
)
OUTPUT INSERTED.Id
VALUES
(
    @WarehouseTypeId, @LocationId, @TakeTypeId, @StrategyId,
    @Freeze, @Status, @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, @IsActive
);";

            const string insertLinesSql = @"
INSERT INTO StockTakeLines
(
    StockTakeId, ItemId, AvailableQty, CountedQty, VarianceQty,
    Barcode, Remarks, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
)
VALUES
(
    @StockTakeId, @ItemId, @AvailableQty, @CountedQty, @VarianceQty,
    @Barcode, @Remarks, @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, @IsActive
);";

            // Make sure connection is open if your factory doesn’t do it for you.
            if (Connection.State != ConnectionState.Open) Connection.Open();

            using var tx = Connection.BeginTransaction();

            try
            {
                // 1) Insert header
                var newId = await Connection.ExecuteScalarAsync<int>(
                    insertHeaderSql, new
                    {
                        stockTake.WarehouseTypeId,
                        stockTake.LocationId,
                        stockTake.TakeTypeId,
                        stockTake.StrategyId,                // can be null
                        stockTake.Freeze,
                        stockTake.Status,                    // consider byte/enum to match TINYINT
                        stockTake.CreatedBy,
                        stockTake.CreatedDate,
                        stockTake.UpdatedBy,
                        stockTake.UpdatedDate,
                        IsActive = stockTake.IsActive        // default true via model/base
                    },
                    transaction: tx
                );

                // 2) Insert lines (if any)
                if (stockTake.LineItems != null && stockTake.LineItems.Count > 0)
                {
                    // Shape params for Dapper (one exec across IEnumerable)
                    var lineParams = stockTake.LineItems.Select(l => new
                    {
                        StockTakeId = newId,
                        l.ItemId,
                        l.AvailableQty,
                        CountedQty = l.CountedQty,                // can be null at plan stage
                        VarianceQty = l.CountedQty.HasValue
                                       ? (decimal?)(l.CountedQty.Value - l.AvailableQty)
                                       : null,
                        l.Barcode,
                        l.Remarks,
                        CreatedBy = stockTake.CreatedBy,
                        CreatedDate = stockTake.CreatedDate,
                        UpdatedBy = stockTake.UpdatedBy,
                        UpdatedDate = stockTake.UpdatedDate,
                        IsActive = true
                    });

                    await Connection.ExecuteAsync(insertLinesSql, lineParams, transaction: tx);
                }

                // 3) Commit
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
            if (Connection.State != ConnectionState.Open) Connection.Open();

            var now = DateTime.UtcNow;
            updatedStockTake.UpdatedDate = now;

            const string updateHeaderSql = @"
UPDATE StockTake
SET
    WarehouseTypeId = @WarehouseTypeId,
    LocationId      = @LocationId,
    TakeTypeId      = @TakeTypeId,
    StrategyId      = @StrategyId,
    Freeze          = @Freeze,
    Status          = @Status,
    UpdatedBy       = @UpdatedBy,
    UpdatedDate     = @UpdatedDate
WHERE Id = @Id;";

            const string updateLineSql = @"
UPDATE StockTakeLines
SET
    ItemId       = @ItemId,
    AvailableQty = @AvailableQty,
    CountedQty   = @CountedQty,
    VarianceQty  = @VarianceQty,
    Barcode      = @Barcode,
    Remarks      = @Remarks,
    UpdatedBy    = @UpdatedBy,
    UpdatedDate  = @UpdatedDate,
    IsActive     = 1
WHERE Id = @Id AND StockTakeId = @StockTakeId;";

            const string insertLineSql = @"
INSERT INTO StockTakeLines
(
    StockTakeId, ItemId, AvailableQty, CountedQty, VarianceQty,
    Barcode, Remarks, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
)
VALUES
(
    @StockTakeId, @ItemId, @AvailableQty, @CountedQty, @VarianceQty,
    @Barcode, @Remarks, @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, 1
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            const string softDeleteMissingSql = @"
UPDATE StockTakeLines
SET IsActive = 0, UpdatedBy = @UpdatedBy, UpdatedDate = @UpdatedDate
WHERE StockTakeId = @StockTakeId
  AND IsActive = 1
  AND (@KeepIdsCount = 0 OR Id NOT IN @KeepIds);";

            using var tx = Connection.BeginTransaction();
            try
            {
                // 1) Update header
                await Connection.ExecuteAsync(updateHeaderSql, new
                {
                    updatedStockTake.WarehouseTypeId,
                    updatedStockTake.LocationId,
                    updatedStockTake.TakeTypeId,
                    updatedStockTake.StrategyId,
                    updatedStockTake.Freeze,
                    updatedStockTake.Status,     // byte/enum recommended
                    updatedStockTake.UpdatedBy,
                    updatedStockTake.UpdatedDate,
                    updatedStockTake.Id
                }, tx);

                // 2) Upsert lines
                var keepIds = new List<int>();
                if (updatedStockTake.LineItems != null && updatedStockTake.LineItems.Count > 0)
                {
                    foreach (var l in updatedStockTake.LineItems)
                    {
                        decimal? variance = l.CountedQty is null ? (decimal?)null : (l.CountedQty - l.AvailableQty);

                        if (l.Id > 0) // UPDATE existing
                        {
                            await Connection.ExecuteAsync(updateLineSql, new
                            {
                                Id = l.Id,
                                StockTakeId = updatedStockTake.Id,
                                l.ItemId,
                                l.AvailableQty,
                                CountedQty = l.CountedQty,
                                VarianceQty = variance,
                                l.Barcode,
                                l.Remarks,
                                UpdatedBy = updatedStockTake.UpdatedBy,
                                UpdatedDate = now
                            }, tx);

                            keepIds.Add(l.Id);
                        }
                        else // INSERT new
                        {
                            var newLineId = await Connection.ExecuteScalarAsync<int>(insertLineSql, new
                            {
                                StockTakeId = updatedStockTake.Id,
                                l.ItemId,
                                l.AvailableQty,
                                CountedQty = l.CountedQty,
                                VarianceQty = variance,
                                l.Barcode,
                                l.Remarks,
                                CreatedBy = updatedStockTake.UpdatedBy ?? updatedStockTake.CreatedBy, // ← current user preferred
                                CreatedDate = now,                                                     // ← use now
                                UpdatedBy = updatedStockTake.UpdatedBy,
                                UpdatedDate = now
                            }, tx);

                            keepIds.Add(newLineId);
                        }
                    }
                }

                // 3) Soft-delete missing lines
                var keepIdsParam = keepIds.Count == 0 ? new int[] { -1 } : keepIds.ToArray();

                await Connection.ExecuteAsync(softDeleteMissingSql, new
                {
                    StockTakeId = updatedStockTake.Id,
                    KeepIds = keepIdsParam,
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





        public async Task DeactivateAsync(int id)
        {
            const string query = "UPDATE StockTake SET IsActive = 0 WHERE ID = @id";
            await Connection.ExecuteAsync(query, new { ID = id });
        }
    }
}
