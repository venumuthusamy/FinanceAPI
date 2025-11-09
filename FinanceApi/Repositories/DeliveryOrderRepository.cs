// File: Repositories/DeliveryOrderRepository.cs
using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using static FinanceApi.ModelDTO.DeliveryOrderDtos;

namespace FinanceApi.Repositories
{
    public class DeliveryOrderRepository : DynamicRepository, IDeliveryOrderRepository
    {
        public DeliveryOrderRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory) { }

        // -------------------- CREATE --------------------
        public async Task<int> CreateAsync(DoCreateRequest req, int userId)
        {
            if (req.DriverId == 0)
                throw new ArgumentException("DriverId is required.");

            await EnsureOpenAsync().ConfigureAwait(false);

            // This DO lines (for snapshot include)
            var thisDoPairs = (req.Lines ?? new List<DoCreateRequest.DoCreateLine>())
                .Where(l => l.SoLineId.HasValue && l.Qty > 0)
                .Select(l => (SoLineId: l.SoLineId!.Value, Qty: l.Qty))
                .ToList();

            // Precompute IsPosted (includes this DO quantities)
            bool isPosted = false;
            if (req.SoId.HasValue && thisDoPairs.Count > 0)
            {
                var ids = thisDoPairs.Select(p => p.SoLineId).Distinct().ToArray();
                var snap = await GetSoLineSnapshotIncludingThisDoAsync(
                    ids,
                    thisDoPairs,
                    excludeDoId: null
                ).ConfigureAwait(false);

                isPosted = snap.Values.All(s => DecimalsEqual(s.PendingAfter, 0m));
            }

            // Generate DoNumber with retry if unique collision
            string doNumber = await GenerateNextDoNumberWithRetryAsync().ConfigureAwait(false);

            // Insert header
            var doId = await Connection.QuerySingleAsync<int>(@"
INSERT INTO dbo.DeliveryOrder
(DoNumber, Status, SoId, PackId, DriverId, VehicleId, RouteName, DeliveryDate,
 PodFileUrl, IsPosted, CreatedBy, CreatedOn)
OUTPUT INSERTED.Id
VALUES
(@DoNumber, 0, @SoId, @PackId, @DriverId, @VehicleId, @RouteName, @DeliveryDate,
 NULL, @IsPosted, @UserId, SYSUTCDATETIME());",
                new
                {
                    DoNumber = doNumber,
                    req.SoId,
                    req.PackId,
                    req.DriverId,
                    req.VehicleId,
                    req.RouteName,
                    req.DeliveryDate,
                    IsPosted = isPosted ? 1 : 0,
                    UserId = userId
                }).ConfigureAwait(false);

            // Insert lines
            const string insLine = @"
INSERT INTO dbo.DeliveryOrderLine
(DoId, SoLineId, PackLineId, ItemId, ItemName, Uom, Qty, Notes)
VALUES
(@DoId, @SoLineId, @PackLineId, @ItemId, @ItemName, @Uom, @Qty, @Notes);";

            foreach (var l in req.Lines ?? Enumerable.Empty<DoCreateRequest.DoCreateLine>())
            {
                await Connection.ExecuteAsync(insLine, new
                {
                    DoId = doId,
                    l.SoLineId,
                    l.PackLineId,
                    l.ItemId,
                    ItemName = l.ItemName ?? string.Empty,
                    l.Uom,
                    l.Qty,
                    l.Notes
                }).ConfigureAwait(false);
            }

            return doId;
        }

        // -------------------- READ --------------------
        public Task<DoHeaderDto?> GetHeaderAsync(int id)
            => Connection.QuerySingleOrDefaultAsync<DoHeaderDto>(@"
SELECT DO.Id, DO.DoNumber, DO.Status, DO.SoId, DO.PackId,
       DO.DriverId,
       DO.VehicleId, DO.RouteName, DO.DeliveryDate,
       DO.PodFileUrl, DO.IsPosted
FROM dbo.DeliveryOrder DO
WHERE DO.Id=@id;", new { id });

        public Task<IEnumerable<DoLineDto>> GetLinesAsync(int doId)
            => Connection.QueryAsync<DoLineDto>(@"
SELECT Id, DoId, SoLineId, PackLineId, ItemId, ItemName, Uom, Qty, Notes
FROM dbo.DeliveryOrderLine
WHERE DoId=@doId;", new { doId });

        public Task<IEnumerable<DoHeaderDto>> GetAllAsync()
            => Connection.QueryAsync<DoHeaderDto>(@"
SELECT d.Id, DoNumber, d.Status, SoId, PackId, d.DriverId, VehicleId, RouteName,
      d.DeliveryDate, PodFileUrl, IsPosted , s.SalesOrderNo
FROM dbo.DeliveryOrder as d 
inner join SalesOrder as s on s.id= d.SoId
ORDER BY Id DESC;");

        // -------------------- UPDATE HEADER --------------------
        public async Task UpdateHeaderAsync(int id, DoUpdateHeaderRequest req, int userId)
        {
            await EnsureOpenAsync().ConfigureAwait(false);

            await Connection.ExecuteAsync(@"
UPDATE dbo.DeliveryOrder SET
  DriverId=@DriverId,
  VehicleId=@VehicleId,
  RouteName=@RouteName,
  DeliveryDate=@DeliveryDate,
  UpdatedBy=@UserId,
  UpdatedOn=SYSUTCDATETIME()
WHERE Id=@Id;",
                new
                {
                    Id = id,
                    req.DriverId,
                    req.VehicleId,
                    req.RouteName,
                    req.DeliveryDate,
                    UserId = userId
                }).ConfigureAwait(false);

            await RecalcAndPersistIsPostedAsync(id).ConfigureAwait(false);
        }

        // -------------------- ADD LINE --------------------
        public async Task<int> AddLineAsync(DoAddLineRequest req, int userId)
        {
            await EnsureOpenAsync().ConfigureAwait(false);

            var lineId = await Connection.QuerySingleAsync<int>(@"
INSERT INTO dbo.DeliveryOrderLine
(DoId, SoLineId, PackLineId, ItemId, ItemName, Uom, Qty, Notes)
OUTPUT INSERTED.Id
VALUES
(@DoId, @SoLineId, @PackLineId, @ItemId, @ItemName, @Uom, @Qty, @Notes);", req)
                .ConfigureAwait(false);

            await RecalcAndPersistIsPostedAsync(req.DoId).ConfigureAwait(false);
            return lineId;
        }

        // -------------------- REMOVE LINE --------------------
        public async Task RemoveLineAsync(int lineId)
        {
            await EnsureOpenAsync().ConfigureAwait(false);

            var doId = await Connection.QuerySingleOrDefaultAsync<int?>(
                "SELECT DoId FROM dbo.DeliveryOrderLine WHERE Id=@lineId;",
                new { lineId }).ConfigureAwait(false) ?? 0;

            await Connection.ExecuteAsync(
                "DELETE FROM dbo.DeliveryOrderLine WHERE Id=@lineId;",
                new { lineId }).ConfigureAwait(false);

            if (doId > 0)
                await RecalcAndPersistIsPostedAsync(doId).ConfigureAwait(false);
        }

        // -------------------- STATUS --------------------
        public Task SetStatusAsync(int id, int status, int userId)
            => Connection.ExecuteAsync(@"
UPDATE dbo.DeliveryOrder
SET Status=@status, UpdatedBy=@userId, UpdatedOn=SYSUTCDATETIME()
WHERE Id=@id;", new { id, status, userId });

        public Task PostAsync(int id, int userId)
            => Connection.ExecuteAsync(@"
UPDATE dbo.DeliveryOrder
SET Status=4, IsPosted=1, UpdatedBy=@userId, UpdatedOn=SYSUTCDATETIME()
WHERE Id=@id;", new { id, userId });

        // =========================================================
        // ===============  Internal helpers below  ================
        // =========================================================

        // Unique-constraint safe DoNumber generator (no explicit transactions)
        private async Task<string> GenerateNextDoNumberWithRetryAsync(int maxRetries = 5)
        {
            int attempt = 0;
            while (true)
            {
                attempt++;

                var last = await Connection.QueryFirstOrDefaultAsync<string>(@"
SELECT TOP (1) DoNumber
FROM dbo.DeliveryOrder WITH (READPAST)
WHERE DoNumber LIKE 'DO-%'
ORDER BY Id DESC;").ConfigureAwait(false);

                int next = 1;
                if (!string.IsNullOrWhiteSpace(last))
                {
                    var m = Regex.Match(last, @"^DO-(\d+)$");
                    if (m.Success && int.TryParse(m.Groups[1].Value, out var n))
                        next = n + 1;
                }

                string candidate = $"DO-{next:D6}";

                try
                {
                    var exists = await Connection.ExecuteScalarAsync<int>(
                        "SELECT COUNT(1) FROM dbo.DeliveryOrder WHERE DoNumber=@n;",
                        new { n = candidate }).ConfigureAwait(false);

                    if (exists == 0) return candidate;
                }
                catch
                {
                    return candidate; // header insert will fail with 2627 if collision
                }

                if (attempt >= maxRetries) return candidate;
            }
        }

        private sealed class SoLineDeliverySnapshot
        {
            public int SoLineId { get; set; }
            public decimal Ordered { get; set; }
            public decimal DeliveredBefore { get; set; } // excluding this DO
            public decimal DeliveredTotal { get; set; }  // DeliveredBefore + this DO qty
            public decimal PendingBefore { get; set; }   // Ordered - DeliveredBefore
            public decimal PendingAfter { get; set; }    // Ordered - DeliveredTotal
        }

        private sealed class DoLineRow
        {
            public int? SoLineId { get; set; }
            public decimal Qty { get; set; }
        }

        private static bool DecimalsEqual(decimal a, decimal b, decimal tol = 0.0001m)
            => Math.Abs(a - b) <= tol;

        /// Snapshot for each SO line id:
        /// DeliveredBefore = SUM(other DOs); DeliveredTotal = DeliveredBefore + this DO;
        /// PendingAfter = Ordered - DeliveredTotal.
        private async Task<Dictionary<int, SoLineDeliverySnapshot>> GetSoLineSnapshotIncludingThisDoAsync(
            int[] soLineIds,
            IEnumerable<(int SoLineId, decimal Qty)> thisDoLines,
            int? excludeDoId)
        {
            const string sql = @"
SELECT
    l.Id AS SoLineId,
    CAST(l.Quantity AS decimal(18,4)) AS Ordered,
    CAST(ISNULL((
        SELECT SUM(dol.Qty)
        FROM dbo.DeliveryOrderLine dol
        WHERE dol.SoLineId = l.Id
          AND (@excludeDoId IS NULL OR dol.DoId <> @excludeDoId)
    ), 0) AS decimal(18,4)) AS DeliveredBefore
FROM dbo.SalesOrderLines l
WHERE l.Id IN @ids;";

            var baseRows = await Connection.QueryAsync<(int SoLineId, decimal Ordered, decimal DeliveredBefore)>(
                sql, new { ids = soLineIds, excludeDoId }).ConfigureAwait(false);

            var thisDoMap = thisDoLines
                .GroupBy(x => x.SoLineId)
                .ToDictionary(g => g.Key, g => g.Sum(v => v.Qty));

            var dict = new Dictionary<int, SoLineDeliverySnapshot>(soLineIds.Length);
            foreach (var r in baseRows)
            {
                var thisQty = thisDoMap.TryGetValue(r.SoLineId, out var q) ? q : 0m;
                var deliveredTotal = r.DeliveredBefore + thisQty;
                var pendingBefore = Math.Max(r.Ordered - r.DeliveredBefore, 0m);
                var pendingAfter = Math.Max(r.Ordered - deliveredTotal, 0m);

                dict[r.SoLineId] = new SoLineDeliverySnapshot
                {
                    SoLineId = r.SoLineId,
                    Ordered = r.Ordered,
                    DeliveredBefore = r.DeliveredBefore,
                    DeliveredTotal = deliveredTotal,
                    PendingBefore = pendingBefore,
                    PendingAfter = pendingAfter
                };
            }
            return dict;
        }

        /// Recalculate IsPosted after changes (no transactions). Includes this DO’s qty.
        private async Task RecalcAndPersistIsPostedAsync(int doId)
        {
            var soId = await Connection.QuerySingleOrDefaultAsync<int?>(
                "SELECT SoId FROM dbo.DeliveryOrder WHERE Id=@doId;",
                new { doId }).ConfigureAwait(false);

            var lines = (await Connection.QueryAsync<DoLineRow>(@"
SELECT SoLineId, Qty
FROM dbo.DeliveryOrderLine
WHERE DoId=@doId;", new { doId }).ConfigureAwait(false)).ToList();

            bool isPosted = false;

            if (soId.HasValue && lines.Count > 0 && lines.All(x => x.SoLineId.HasValue && x.Qty > 0))
            {
                var grouped = lines
                    .GroupBy(x => x.SoLineId!.Value)
                    .ToDictionary(g => g.Key, g => g.Sum(v => v.Qty));

                var ids = grouped.Keys.ToArray();

                var snap = await GetSoLineSnapshotIncludingThisDoAsync(
                    ids,
                    grouped.Select(kvp => (kvp.Key, kvp.Value)),
                    excludeDoId: doId
                ).ConfigureAwait(false);

                isPosted = snap.Values.All(s => DecimalsEqual(s.PendingAfter, 0m));
            }

            await Connection.ExecuteAsync(@"
UPDATE dbo.DeliveryOrder
SET IsPosted=@isPosted, UpdatedOn=SYSUTCDATETIME()
WHERE Id=@doId;",
                new { doId, isPosted }).ConfigureAwait(false);
        }

        private async Task EnsureOpenAsync()
        {
            if (Connection.State != ConnectionState.Open)
            {
                if (Connection is System.Data.Common.DbConnection dbc)
                    await dbc.OpenAsync().ConfigureAwait(false);
                else
                    Connection.Open();
            }
        }
        // Reuse your existing helpers inside DeliveryOrderRepository
        public async Task<IEnumerable<object>> GetSoRedeliveryViewAsync(int doId, int soId)
        {
            // SO lines with Ordered
            var soLines = await Connection.QueryAsync<(int SoLineId, int? ItemId, string ItemName, string Uom, decimal Ordered)>(@"
        SELECT l.Id AS SoLineId, l.ItemId, l.ItemName, l.Uom, CAST(l.Quantity AS decimal(18,4)) AS Ordered
        FROM dbo.SalesOrderLines l WHERE l.SalesOrderId=@soId;", new { soId });

            // Delivered by OTHER DOs
            var other = await Connection.QueryAsync<(int SoLineId, decimal Qty)>(@"
        SELECT SoLineId, SUM(Qty) AS Qty
        FROM dbo.DeliveryOrderLine
        WHERE SoLineId IS NOT NULL AND DoId<>@doId
        GROUP BY SoLineId;", new { doId });
            var otherMap = other.ToDictionary(x => x.SoLineId, x => x.Qty);

            // Delivered on THIS DO
            var thisDo = await Connection.QueryAsync<(int SoLineId, decimal Qty)>(@"
        SELECT SoLineId, SUM(Qty) AS Qty
        FROM dbo.DeliveryOrderLine
        WHERE DoId=@doId AND SoLineId IS NOT NULL
        GROUP BY SoLineId;", new { doId });
            var thisMap = thisDo.ToDictionary(x => x.SoLineId, x => x.Qty);

            var list = new List<object>();
            foreach (var s in soLines)
            {
                var before = otherMap.TryGetValue(s.SoLineId, out var ob) ? ob : 0m;
                var onThis = thisMap.TryGetValue(s.SoLineId, out var tb) ? tb : 0m;
                var pending = Math.Max(s.Ordered - (before + onThis), 0m);

                list.Add(new
                {
                    soLineId = s.SoLineId,
                    itemId = s.ItemId,
                    itemName = s.ItemName,
                    uom = s.Uom,
                    ordered = s.Ordered,
                    deliveredBefore = before,
                    deliveredOnThisDo = onThis,
                    pending = pending
                });
            }
            return list;
        }

    }
}
