using System.Data;
using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.Data.SqlClient;

namespace FinanceApi.Repositories
{
    public class CreditNoteRepository : DynamicRepository, ICreditNoteRepository
    {
        public CreditNoteRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

        // ===================== LIST =====================
        public async Task<IEnumerable<CreditNoteDTO>> GetAllAsync()
        {
            const string hdrSql = @"
SELECT
    cn.Id, cn.CreditNoteNo, cn.DoId, d.DoNumber,
    cn.SiId, si.InvoiceNo AS SiNumber,
    cn.CustomerId, ISNULL(c.CustomerName,'') AS CustomerName,
    cn.CreditNoteDate,
    CAST(cn.Status AS tinyint) AS Status,
    ISNULL(cn.Subtotal,0) AS Subtotal,
    cn.IsActive
FROM dbo.CreditNote cn
LEFT JOIN dbo.DeliveryOrder d    ON d.Id = cn.DoId
LEFT JOIN dbo.SalesInvoice si    ON si.Id = cn.SiId
LEFT JOIN dbo.Customer c         ON c.Id = cn.CustomerId
WHERE cn.IsActive = 1
ORDER BY cn.Id DESC;";

            var headers = (await Connection.QueryAsync<CreditNoteDTO>(hdrSql)).ToList();
            if (headers.Count == 0) return headers;

            var ids = headers.Select(h => h.Id).ToArray();

            const string lineSql = @"
SELECT
    l.Id, l.CreditNoteId,
    l.DoId AS DId, l.SiId,
    l.ItemId, l.ItemName, l.Uom,
    l.DeliveredQty, l.ReturnedQty,
    l.UnitPrice, l.DiscountPct, l.TaxCodeId,
    l.LineNet, l.ReasonId, l.RestockDispositionId,
    l.IsActive
FROM dbo.CreditNoteLine l
WHERE l.CreditNoteId IN @Ids AND l.IsActive = 1
ORDER BY l.Id;";

            var lines = (await Connection.QueryAsync<CreditNoteLineDTO>(lineSql, new { Ids = ids })).ToList();
            var map = headers.ToDictionary(h => h.Id);
            foreach (var ln in lines)
                if (map.TryGetValue(ln.CreditNoteId, out var parent)) parent.Lines.Add(ln);

            return headers;
        }

        // ===================== GET ONE =====================
        public async Task<CreditNoteDTO?> GetByIdAsync(int id)
        {
            const string hdrSql = @"
SELECT TOP (1)
    cn.Id, cn.CreditNoteNo, cn.DoId, d.DoNumber,
    cn.SiId, si.InvoiceNo AS SiNumber,
    cn.CustomerId, ISNULL(c.CustomerName,'') AS CustomerName,
    cn.CreditNoteDate,
    CAST(cn.Status AS tinyint) AS Status,
    ISNULL(cn.Subtotal,0) AS Subtotal,
    cn.IsActive
FROM dbo.CreditNote cn
LEFT JOIN dbo.DeliveryOrder d ON d.Id = cn.DoId
LEFT JOIN dbo.SalesInvoice si ON si.Id = cn.SiId
LEFT JOIN dbo.Customer c      ON c.Id = cn.CustomerId
WHERE cn.Id = @Id AND cn.IsActive = 1;";

            var head = await Connection.QueryFirstOrDefaultAsync<CreditNoteDTO>(hdrSql, new { Id = id });
            if (head is null) return null;

            const string lineSql = @"
SELECT
    l.Id, l.CreditNoteId,
    l.DoId AS DId, l.SiId,
    l.ItemId, l.ItemName, l.Uom,
    l.DeliveredQty, l.ReturnedQty,
    l.UnitPrice, l.DiscountPct, l.TaxCodeId,
    l.LineNet, l.ReasonId, l.RestockDispositionId,l.WarehouseId,l.SupplierId,l.BinId,
    l.IsActive
FROM dbo.CreditNoteLine l
WHERE l.CreditNoteId = @Id AND l.IsActive = 1
ORDER BY l.Id;";

            var lines = await Connection.QueryAsync<CreditNoteLineDTO>(lineSql, new { Id = id });
            head.Lines = lines.ToList();
            return head;
        }

        // ===================== CREATE =====================
        public async Task<int> CreateAsync(CreditNote cn)
        {
            if (cn is null) throw new ArgumentNullException(nameof(cn));

            var now = DateTime.UtcNow;
            if (cn.CreatedDate == default) cn.CreatedDate = now;
            if (cn.UpdatedDate == null) cn.UpdatedDate = now;

            // --- DO context WITHOUT using d.SoId or d.Sold ---
            // Customer via: DO -> DO Line -> SalesOrderLines -> SalesOrder -> Customer
            // SI via: latest SalesInvoice where SourceType=2 and DoId = DO.Id
            const string doInfoSql = @"
SELECT
    d.Id               AS DoId,
    d.DoNumber,
    d.DeliveryDate,
    soctx.SoId,
    soctx.CustomerId,
    soctx.CustomerName,
    sictx.SiId,
    sictx.SiNumber
FROM dbo.DeliveryOrder d
OUTER APPLY (
    SELECT TOP (1)
        so.Id          AS SoId,
        so.CustomerId  AS CustomerId,
        c.CustomerName AS CustomerName
    FROM dbo.DeliveryOrderLine dl
    JOIN dbo.SalesOrderLines  sol ON sol.Id = dl.SoLineId
    JOIN dbo.SalesOrder       so  ON so.Id = sol.SalesOrderId
    LEFT JOIN dbo.Customer    c   ON c.Id = so.CustomerId
    WHERE dl.DoId = d.Id
    ORDER BY dl.Id
) AS soctx
OUTER APPLY (
    SELECT TOP (1)
        s.Id        AS SiId,
        s.InvoiceNo AS SiNumber
    FROM dbo.SalesInvoice s
    WHERE s.IsActive = 1
      AND s.SourceType = 2
      AND s.DoId = d.Id
    ORDER BY s.Id DESC
) AS sictx
WHERE d.Id = @DoId;";

            const string insertHdr = @"
INSERT INTO dbo.CreditNote
(
    CreditNoteNo, DoId, DoNumber, SiId, SiNumber,
    CustomerId, CustomerName, CreditNoteDate,
    Status, Subtotal,
    CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
)
OUTPUT INSERTED.Id
VALUES
(
    @CreditNoteNo, @DoId, @DoNumber, @SiId, @SiNumber,
    @CustomerId, @CustomerName,@CreditNoteDate,
    @Status, @Subtotal,
    @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, 1
);";

            const string insertLine = @"
INSERT INTO dbo.CreditNoteLine
(
    CreditNoteId, DoId, SiId,
    ItemId, ItemName, Uom,
    DeliveredQty, ReturnedQty,
    UnitPrice, DiscountPct, TaxCodeId,
    LineNet, ReasonId, RestockDispositionId,WarehouseId,SupplierId,BinId,
    IsActive
)
VALUES
(
    @CreditNoteId, @DoId, @SiId,
    @ItemId, @ItemName, @Uom,
    @DeliveredQty, @ReturnedQty,
    @UnitPrice, @DiscountPct, @TaxCodeId,
    @LineNet, @ReasonId, @RestockDispositionId,@WarehouseId,@SupplierId,@BinId,
    1
);";

            const string nextNo = @"
DECLARE @n INT;
SELECT @n = ISNULL(MAX(TRY_CONVERT(int, RIGHT(CreditNoteNo, 6))), 0) + 1
FROM dbo.CreditNote WITH (UPDLOCK, HOLDLOCK);
SELECT CONCAT('CN-', RIGHT(CONCAT(REPLICATE('0', 6), @n), 6));";

            var conn = Connection;
            if (conn.State != ConnectionState.Open)
                await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();
            try
            {
                // 1) Pull context
                var info = await conn.QueryFirstOrDefaultAsync(
                    new CommandDefinition(doInfoSql, new { DoId = cn.DoId }, transaction: tx)
                );
                if (info is null)
                    throw new InvalidOperationException("Delivery Order not found.");

                // 2) Fill header from context (do NOT assume DO has CustomerId/SiId columns)
                cn.DoNumber ??= (string?)info.DoNumber;
                cn.SiId = cn.SiId != 0 ? cn.SiId : (int?)info.SiId ?? 0;
                cn.SiNumber ??= (string?)info.SiNumber;
                cn.CustomerId = cn.CustomerId != 0 ? cn.CustomerId : (int?)info.CustomerId ?? 0;
                cn.CustomerName ??= (string?)info.CustomerName ?? "";
               

                // 3) Generate CN number
                cn.CreditNoteNo = await conn.ExecuteScalarAsync<string>(nextNo, transaction: tx);

                // 4) Recompute line nets + subtotal server-side
                if (cn.Lines is { Count: > 0 })
                {
                    foreach (var l in cn.Lines)
                    {
                        var net = l.ReturnedQty * l.UnitPrice * (1 - (l.DiscountPct / 100m));
                        l.LineNet = Math.Round(net, 2, MidpointRounding.AwayFromZero);
                    }
                    cn.Subtotal = Math.Round(cn.Lines.Sum(x => x.LineNet), 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    cn.Subtotal = 0m;
                }

                // 5) Insert header
                var newId = await conn.ExecuteScalarAsync<int>(insertHdr, new
                {
                    cn.CreditNoteNo,
                    cn.DoId,
                    cn.DoNumber,
                    SiId = cn.SiId,
                    cn.SiNumber,
                    cn.CustomerId,
                    cn.CustomerName,
                    cn.CreditNoteDate,
                    Status = (byte)cn.Status,
                    cn.Subtotal,
                    cn.CreatedBy,
                    cn.CreatedDate,
                    cn.UpdatedBy,
                    UpdatedDate = cn.UpdatedDate ?? now
                }, tx);

                // 6) Insert lines
                if (cn.Lines is { Count: > 0 })
                {
                    var rows = cn.Lines.Select(l => new
                    {
                        CreditNoteId = newId,
                        DoId = (int?)cn.DoId,
                        SiId = (int?)cn.SiId,
                        l.ItemId,
                        l.ItemName,
                        Uom = l.Uom,
                        l.DeliveredQty,
                        l.ReturnedQty,
                        l.UnitPrice,
                        l.DiscountPct,
                        l.TaxCodeId,
                        l.LineNet,
                        l.ReasonId,
                        l.RestockDispositionId,
                        l.WarehouseId,
                        l.SupplierId,
                        l.BinId
                    });
                    await conn.ExecuteAsync(insertLine, rows, tx);
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



        // ===================== UPDATE =====================
        public async Task UpdateAsync(CreditNote cn)
        {
            if (cn is null) throw new ArgumentNullException(nameof(cn));
            var now = DateTime.UtcNow;

            const string updHdr = @"
UPDATE dbo.CreditNote
SET
    DoId=@DoId, DoNumber=@DoNumber,
    SiId=@SiId, SiNumber=@SiNumber,
    CustomerId=@CustomerId, CustomerName=@CustomerName,
    CreditNoteDate=@CreditNoteDate,
    Status=@Status, Subtotal=@Subtotal,
    UpdatedBy=@UpdatedBy, UpdatedDate=@UpdatedDate
WHERE Id=@Id AND IsActive=1;";

            const string updLine = @"
UPDATE dbo.CreditNoteLine
SET
    DoId=@DoId, SiId=@SiId,
    ItemId=@ItemId, ItemName=@ItemName, Uom=@Uom,
    DeliveredQty=@DeliveredQty, ReturnedQty=@ReturnedQty,
    UnitPrice=@UnitPrice, DiscountPct=@DiscountPct, TaxCodeId=@TaxCodeId,
    LineNet=@LineNet, ReasonId=@ReasonId, RestockDispositionId=@RestockDispositionId,WarehouseId = @WarehouseId,SupplierId=@SupplierId,BinId=@BinId,
    IsActive=1
WHERE Id=@Id AND CreditNoteId=@CreditNoteId;";

            const string insLine = @"
INSERT INTO dbo.CreditNoteLine
(
    CreditNoteId, DoId, SiId,
    ItemId, ItemName, Uom,
    DeliveredQty, ReturnedQty,
    UnitPrice, DiscountPct, TaxCodeId,
    LineNet, ReasonId, RestockDispositionId,WarehouseId,SupplierId,BinId, IsActive
)
OUTPUT INSERTED.Id
VALUES
(
    @CreditNoteId, @DoId, @SiId,
    @ItemId, @ItemName, @Uom,
    @DeliveredQty, @ReturnedQty,
    @UnitPrice, @DiscountPct, @TaxCodeId,
    @LineNet, @ReasonId, @RestockDispositionId,@WarehouseId,@SupplierId,@BinId, 1
);";

            const string softDeleteMissing = @"
UPDATE dbo.CreditNoteLine
SET IsActive=0, ReasonId=ReasonId, RestockDispositionId=RestockDispositionId
WHERE CreditNoteId=@CreditNoteId AND IsActive=1
  AND (@KeepIdsCount=0 OR Id NOT IN @KeepIds);";

            // recompute server-side
            foreach (var l in cn.Lines)
            {
                var net = l.ReturnedQty * l.UnitPrice * (1 - (l.DiscountPct / 100m));
                l.LineNet = Math.Round(net, 2, MidpointRounding.AwayFromZero);
            }
            cn.Subtotal = Math.Round(cn.Lines.Sum(x => x.LineNet), 2, MidpointRounding.AwayFromZero);

            var conn = Connection;
            if (conn.State != ConnectionState.Open) await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();
            try
            {
                await conn.ExecuteAsync(updHdr, new
                {
                    cn.DoId,
                    cn.DoNumber,
                    cn.SiId,
                    cn.SiNumber,
                    cn.CustomerId,
                    cn.CustomerName,
                    cn.CreditNoteDate,
                    Status = (byte)cn.Status,
                    cn.Subtotal,
                    cn.UpdatedBy,
                    UpdatedDate = now,
                    cn.Id
                }, tx);

                var keepIds = new List<int>();
                if (cn.Lines?.Count > 0)
                {
                    foreach (var l in cn.Lines)
                    {
                        if (l.Id > 0)
                        {
                            await conn.ExecuteAsync(updLine, new
                            {
                                l.Id,
                                CreditNoteId = cn.Id,
                                DoId = (int?)cn.DoId,
                                SiId = (int?)cn.SiId,
                                l.ItemId,
                                l.ItemName,
                                Uom = l.Uom,
                                l.DeliveredQty,
                                l.ReturnedQty,
                                l.UnitPrice,
                                l.DiscountPct,
                                l.TaxCodeId,
                                l.LineNet,
                                l.ReasonId,
                                l.RestockDispositionId,
                                l.WarehouseId,
                                l.SupplierId,
                                l.BinId
                            }, tx);
                            keepIds.Add(l.Id);
                        }
                        else
                        {
                            var newLineId = await conn.ExecuteScalarAsync<int>(insLine, new
                            {
                                CreditNoteId = cn.Id,
                                DoId = (int?)cn.DoId,
                                SiId = (int?)cn.SiId,
                                l.ItemId,
                                l.ItemName,
                                Uom = l.Uom,
                                l.DeliveredQty,
                                l.ReturnedQty,
                                l.UnitPrice,
                                l.DiscountPct,
                                l.TaxCodeId,
                                l.LineNet,
                                l.ReasonId,
                                l.RestockDispositionId,
                                l.WarehouseId,
                                l.SupplierId,
                                l.BinId
                            }, tx);
                            keepIds.Add(newLineId);
                        }
                    }
                }

                await conn.ExecuteAsync(softDeleteMissing, new
                {
                    CreditNoteId = cn.Id,
                    KeepIds = keepIds.Count == 0 ? new[] { -1 } : keepIds.ToArray(),
                    KeepIdsCount = keepIds.Count
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
            const string sqlHeader = @"
UPDATE dbo.CreditNote
SET IsActive=0, UpdatedBy=@UpdatedBy, UpdatedDate=SYSUTCDATETIME()
WHERE Id=@Id AND IsActive=1;";

            const string sqlLines = @"
UPDATE dbo.CreditNoteLine
SET IsActive=0
WHERE CreditNoteId=@Id AND IsActive=1;";

            var conn = Connection;
            if (conn.State != ConnectionState.Open) await (conn as SqlConnection)!.OpenAsync();

            using var tx = conn.BeginTransaction();
            try
            {
                var n = await conn.ExecuteAsync(sqlHeader, new { Id = id, UpdatedBy = updatedBy }, tx);
                if (n == 0) throw new KeyNotFoundException("Credit note not found.");
                await conn.ExecuteAsync(sqlLines, new { Id = id }, tx);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // ===================== DO → SI LINE ENRICH =====================
        // Prefer exact DO line → SI link (SourceType=2 + SourceLineId)
        // Fallback: same SI + same ItemId
        public async Task<IEnumerable<object>> GetDoLinesAsync(int doId)
        {
            const string sql = @"
SELECT 
    dl.Id            AS DoLineId,
    dl.ItemId,
    i.ItemName,
    dl.Uom,
    dl.WarehouseId,
    dl.BinId,
    dl.SupplierId,
    dl.Qty                      AS QtyDelivered,
    COALESCE(si.UnitPrice, 0)   AS UnitPrice,
    COALESCE(si.DiscountPct, 0) AS DiscountPct,
    si.TaxCodeId
FROM dbo.DeliveryOrderLine dl
JOIN dbo.DeliveryOrder d    ON d.Id = dl.DoId
LEFT JOIN dbo.Item i        ON i.Id = dl.ItemId
OUTER APPLY (
    SELECT TOP (1) sil.UnitPrice, sil.DiscountPct, sil.TaxCodeId
    FROM dbo.SalesInvoiceLine sil
    WHERE (sil.SourceType = 2 AND sil.SourceLineId = dl.Id)
       OR (sil.SourceType = 1 AND sil.SourceLineId = dl.SoLineId)
       OR (sil.ItemId = dl.ItemId)
    ORDER BY
        CASE 
          WHEN (sil.SourceType = 2 AND sil.SourceLineId = dl.Id) THEN 0
          WHEN (sil.SourceType = 1 AND sil.SourceLineId = dl.SoLineId) THEN 1
          ELSE 2
        END,
        sil.Id DESC
) si
WHERE dl.DoId = @doId
ORDER BY dl.Id;";
            return await Connection.QueryAsync(sql, new { doId });
        }

    }
}
