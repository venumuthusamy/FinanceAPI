using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Interfaces;
using System.Data;

namespace FinanceApi.Repositories
{
    public class ItemMasterRepository : DynamicRepository, IItemMasterRepository
    {
        public ItemMasterRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

        // ===================== READS =====================

        public async Task<IEnumerable<ItemMasterDTO>> GetAllAsync()

        {

            const string sql = @"

SELECT 

    i.Id,

    i.Sku,

    i.Name,

    i.Category,

    i.Uom,

    i.CostingMethodId,

    i.TaxCodeId,

    i.Specs,

    i.PictureUrl,

    i.IsActive,

    i.CreatedBy,

    i.CreatedDate,

    i.UpdatedBy,

    i.UpdatedDate,

    SUM(ISNULL(inv.OnHand, 0))     AS OnHand,

    SUM(ISNULL(inv.Reserved, 0))   AS Reserved,

    SUM(ISNULL(inv.OnHand, 0)) - SUM(ISNULL(inv.Reserved, 0)) AS Available,

    SUM(ISNULL(inv.Available, 0))  AS Qty

FROM dbo.ItemMaster i

LEFT JOIN dbo.ItemWarehouseStock inv 

    ON inv.ItemId = i.Id

WHERE i.IsActive = 1

GROUP BY 

    i.Id, i.Sku, i.Name, i.Category, i.Uom,

    i.CostingMethodId, i.TaxCodeId, i.Specs,

    i.PictureUrl, i.IsActive, i.CreatedBy,

    i.CreatedDate, i.UpdatedBy, i.UpdatedDate

ORDER BY i.Id DESC;";

            return await Connection.QueryAsync<ItemMasterDTO>(sql);

        }


        public async Task<ItemMasterDTO?> GetByIdAsync(int id)
        {
            const string sql = @"
SELECT i.*,
       ISNULL(inv.OnHand,0)   AS OnHand,
       ISNULL(inv.Reserved,0) AS Reserved,
       ISNULL(inv.OnHand,0) - ISNULL(inv.Reserved,0) AS Available
FROM dbo.ItemMaster i
LEFT JOIN dbo.InventorySummaries inv ON inv.ItemId = i.Id
WHERE i.Id = @Id;";
            return await Connection.QueryFirstOrDefaultAsync<ItemMasterDTO>(sql, new { Id = id });
        }

        // ===================== WRITES =====================

        public async Task<int> CreateAsync(ItemMaster item)
        {
            item.CreatedDate = item.CreatedDate == default ? DateTime.UtcNow : item.CreatedDate;
            item.UpdatedDate = DateTime.UtcNow;
            item.IsActive = true;

            const string sql = @"
INSERT INTO dbo.ItemMaster
 (Sku,Name,Category,Uom,Barcode,CostingMethodId,TaxCodeId,Specs,PictureUrl,IsActive,CreatedDate,UpdatedDate)
OUTPUT INSERTED.Id
VALUES
 (@Sku,@Name,@Category,@Uom,@Barcode,@CostingMethodId,@TaxCodeId,@Specs,@PictureUrl,@IsActive,@CreatedDate,@UpdatedDate);";

            return await Connection.QueryFirstAsync<int>(sql, new
            {
                item.Sku,
                item.Name,
                item.Category,
                item.Uom,
                item.CostingMethodId,
                item.TaxCodeId,
                item.Specs,
                item.PictureUrl,
                item.IsActive,
                item.CreatedDate,
                item.UpdatedDate
            });
        }

        public async Task<long> CreateAsync(ItemMasterUpsertDto dto)
        {
            const string ins = @"
INSERT INTO dbo.ItemMaster
 (Sku,Name,Category,Uom,CostingMethodId,TaxCodeId,Specs,PictureUrl,IsActive,CreatedBy,CreatedDate,UpdatedBy,UpdatedDate,ExpiryDate)
OUTPUT INSERTED.Id
VALUES(@Sku,@Name,@Category,@Uom,@CostingMethodId,@TaxCodeId,@Specs,@PictureUrl,1,@CreatedBy,SYSUTCDATETIME(),@UpdatedBy,SYSUTCDATETIME(),@ExpiryDate);";

            var itemId = await Connection.QueryFirstAsync<long>(ins, new
            {
                dto.Sku,
                dto.Name,
                dto.Category,
                dto.Uom,
                dto.CostingMethodId,
                dto.TaxCodeId,
                dto.Specs,
                dto.PictureUrl,
                dto.CreatedBy,
                dto.UpdatedBy,
                dto.ExpiryDate
            });

            // Prices (now includes Qty)
            if (dto.Prices is not null && dto.Prices.Count > 0)
            {
                const string ip = @"
INSERT INTO dbo.ItemPrice (ItemId,SupplierId,Price,Qty,Barcode)
VALUES (@ItemId,@SupplierId,@Price,@Qty,@Barcode);";
                foreach (var p in dto.Prices)
                {
                    await Connection.ExecuteAsync(ip, new
                    {
                        ItemId = itemId,
                        SupplierId = p.SupplierId,
                        Price = p.Price,
                        Qty = p.Qty ?? 0m,      // NEW
                        Barcode = p.Barcode
                    });
                }
            }

            // Warehouses
            if (dto.ItemStocks is not null && dto.ItemStocks.Count > 0)
            {
                const string iw = @"
INSERT INTO dbo.ItemWarehouseStock
 (ItemId,WarehouseId,BinId,StrategyId,OnHand,Reserved,MinQty,MaxQty,ReorderQty,LeadTimeDays,BatchFlag,SerialFlag,Available,IsApproved,IsTransfered,StockIssueID,IsFullTransfer,IsPartialTransfer)
VALUES(@ItemId,@WarehouseId,@BinId,@StrategyId,@OnHand,@Reserved,@MinQty,@MaxQty,@ReorderQty,@LeadTimeDays,@BatchFlag,@SerialFlag,@Available,@IsApproved,@IsTransfered,@StockIssueID,@IsFullTransfer,@IsPartialTransfer);";

                foreach (var s in dto.ItemStocks)
                {
                    decimal onHand = s.OnHand;
                    decimal reserved = s.Reserved;
                    int computedAvailable = (int)Math.Max(0m, onHand - reserved);

                    await Connection.ExecuteAsync(iw, new
                    {
                        ItemId = itemId,
                        s.WarehouseId,
                        s.BinId,
                        s.StrategyId,
                        OnHand = onHand,
                        Reserved = reserved,
                        MinQty = s.MinQty ?? 0m,
                        MaxQty = s.MaxQty ?? 0m,
                        ReorderQty = s.ReorderQty ?? 0m,
                        LeadTimeDays = s.LeadTimeDays ?? 0,
                        s.BatchFlag,
                        s.SerialFlag,
                        Available = s.Available != 0 ? s.Available : computedAvailable,
                        s.IsApproved,
                        s.IsTransfered,
                        s.StockIssueID,
                        s.IsFullTransfer,
                        s.IsPartialTransfer
                    });
                }
            }

            // BOM
            if (dto.BomLines is not null && dto.BomLines.Count > 0)
            {
                const string ib = @"
INSERT INTO dbo.ItemBom (ItemId, ExistingCost, UnitCost, CreatedBy)
VALUES (@ItemId, @ExistingCost, @UnitCost, @CreatedBy);";

                foreach (var b in dto.BomLines)
                {
                    decimal existingCost = b.ExistingCost;
                    decimal unitCost = (b.UnitCost != 0m) ? b.UnitCost : existingCost;

                    await Connection.ExecuteAsync(ib, new
                    {
                        ItemId = itemId,
                        ExistingCost = existingCost,
                        UnitCost = unitCost,
                        CreatedBy = dto.CreatedBy
                    });
                }
            }

            // Audit
            long? userId = null;
            if (long.TryParse(dto.CreatedBy, out var uid)) userId = uid;

            var newJson = await GetItemSnapshotJsonAsync(itemId);
            await AddAuditAsync(itemId, "CREATE", userId, null, newJson, null);

            return itemId;
        }

        // ============= Update parent + children =============
        public async Task UpdateAsync(ItemMaster item)
        {
            item.UpdatedDate = DateTime.UtcNow;

            const string sql = @"
UPDATE dbo.ItemMaster SET
    Sku=@Sku,
    Name=@Name,
    Category=@Category,
    Uom=@Uom,
    CostingMethodId=@CostingMethodId,
    TaxCodeId=@TaxCodeId,
    Specs=@Specs,
    PictureUrl=@PictureUrl,
    IsActive=@IsActive,
    UpdatedDate=@UpdatedDate,
    ExpiryDate=@ExpiryDate
WHERE Id=@Id;";

            await Connection.ExecuteAsync(sql, new
            {
                item.Id,
                item.Sku,
                item.Name,
                item.Category,
                item.Uom,
                item.CostingMethodId,
                item.TaxCodeId,
                item.Specs,
                item.PictureUrl,
                item.IsActive,
                item.UpdatedDate,
                item.ExpiryDate
            });
        }

        public async Task UpdateAsync(ItemMasterUpsertDto dto)
        {
            var oldJson = await GetItemSnapshotJsonAsync(dto.Id);

            const string up = @"
UPDATE dbo.ItemMaster SET
  Sku=@Sku, Name=@Name, Category=@Category, Uom=@Uom, 
  CostingMethodId=@CostingMethodId, TaxCodeId=@TaxCodeId, Specs=@Specs,
  PictureUrl=@PictureUrl, IsActive=@IsActive, UpdatedDate=SYSUTCDATETIME(), ExpiryDate=@ExpiryDate
WHERE Id=@Id;";
            await Connection.ExecuteAsync(up, new
            {
                dto.Id,
                dto.Sku,
                dto.Name,
                dto.Category,
                dto.Uom,
                dto.CostingMethodId,
                dto.TaxCodeId,
                dto.Specs,
                dto.PictureUrl,
                dto.IsActive,
                dto.ExpiryDate
            });

            // Replace children
            await Connection.ExecuteAsync("DELETE FROM dbo.ItemPrice WHERE ItemId=@Id;", new { dto.Id });
            await Connection.ExecuteAsync("DELETE FROM dbo.ItemWarehouseStock WHERE ItemId=@Id;", new { dto.Id });

            // Prices (with Qty)
            if (dto.Prices is not null && dto.Prices.Count > 0)
            {
                const string ip = @"
INSERT INTO dbo.ItemPrice (ItemId,SupplierId,Price,Qty,Barcode,WarehouseId)
VALUES (@ItemId,@SupplierId,@Price,@Qty,@Barcode,@WarehouseId);";
                foreach (var p in dto.Prices)
                {
                    await Connection.ExecuteAsync(ip, new
                    {
                        ItemId = dto.Id,
                        SupplierId = p.SupplierId,
                        Price = p.Price,
                        Qty = p.Qty ?? 0m,     // NEW
                        Barcode = p.Barcode,
                        WarehouseId = p.WarehouseId,
                    });
                }
            }

            // Stocks
            if (dto.ItemStocks is not null && dto.ItemStocks.Count > 0)
            {
                const string iw = @"
INSERT INTO dbo.ItemWarehouseStock
 (ItemId,WarehouseId,BinId,StrategyId,OnHand,Reserved,MinQty,MaxQty,ReorderQty,LeadTimeDays,BatchFlag,SerialFlag,Available,IsApproved,IsTransfered,StockIssueID,IsFullTransfer,IsPartialTransfer)
VALUES(@ItemId,@WarehouseId,@BinId,@StrategyId,@OnHand,@Reserved,@MinQty,@MaxQty,@ReorderQty,@LeadTimeDays,@BatchFlag,@SerialFlag,@Available,@IsApproved,@IsTransfered,@StockIssueID,@IsFullTransfer,@IsPartialTransfer);";

                foreach (var s in dto.ItemStocks)
                {
                    decimal onHand = s.OnHand;
                    decimal reserved = s.Reserved;
                    int computedAvailable = (int)Math.Max(0m, onHand - reserved);

                    await Connection.ExecuteAsync(iw, new
                    {
                        ItemId = dto.Id,
                        s.WarehouseId,
                        s.BinId,
                        s.StrategyId,
                        OnHand = onHand,
                        Reserved = reserved,
                        MinQty = s.MinQty ?? 0m,
                        MaxQty = s.MaxQty ?? 0m,
                        ReorderQty = s.ReorderQty ?? 0m,
                        LeadTimeDays = s.LeadTimeDays ?? 0,
                        s.BatchFlag,
                        s.SerialFlag,
                        Available = s.Available != 0 ? s.Available : computedAvailable,
                        s.IsApproved,
                        s.IsTransfered,
                        s.StockIssueID,
                        s.IsFullTransfer,
                        s.IsPartialTransfer
                    });
                }
            }

            // BOM
            if (dto.BomLines is not null && dto.BomLines.Count > 0)
            {
                const string ib = @"
INSERT INTO dbo.ItemBom (ItemId, ExistingCost, UnitCost, CreatedBy, SupplierId)
VALUES (@ItemId, @ExistingCost, @UnitCost, @CreatedBy, @SupplierId);";

                foreach (var b in dto.BomLines)
                {
                    decimal existingCost = b.ExistingCost;
                    decimal unitCost = (b.UnitCost != 0m) ? b.UnitCost : existingCost;

                    await Connection.ExecuteAsync(ib, new
                    {
                        ItemId = dto.Id,
                        ExistingCost = existingCost,
                        UnitCost = unitCost,
                        CreatedBy = dto.UpdatedBy,
                        SupplierId = b.SupplierId
                    });
                }
            }

            long? userId = null;
            if (long.TryParse(dto.UpdatedBy, out var uid)) userId = uid;

            var newJson = await GetItemSnapshotJsonAsync(dto.Id);
            await AddAuditAsync(dto.Id, "UPDATE", userId, oldJson, newJson, null);
        }

        // ===================== Inventory + Supplier Price =====================

        public async Task ApplyGrnToInventoryAsync(ApplyGrnRequest req)
        {
            if (req?.Lines == null || req.Lines.Count == 0) return;

            var dbConn = (System.Data.Common.DbConnection)Connection;
            if (dbConn.State != System.Data.ConnectionState.Open)
                await dbConn.OpenAsync();

            using var tx = dbConn.BeginTransaction();
            try
            {
                foreach (var ln in req.Lines)
                {
                    if (string.IsNullOrWhiteSpace(ln.ItemCode))
                        continue;

                    var sku = ln.ItemCode.Trim();
                    var by = req.UpdatedBy ?? "";

                    // Ensure Item exists
                    const string findItemSql = @"SELECT Id FROM dbo.ItemMaster WHERE Sku = @Sku;";
                    var itemId = await dbConn.QueryFirstOrDefaultAsync<long?>(findItemSql, new { Sku = sku }, tx);
                    if (itemId is null)
                    {
                        const string insItemSql = @"
INSERT INTO dbo.ItemMaster
 (Sku, Name, Category, Uom, CostingMethodId, TaxCodeId, Specs, PictureUrl, IsActive, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate)
OUTPUT INSERTED.Id
VALUES (@Sku, @Name, N'Uncategorized', N'EA', NULL, NULL, NULL, NULL, 1, @By, SYSUTCDATETIME(), @By, SYSUTCDATETIME());";
                        itemId = await dbConn.QueryFirstAsync<long>(insItemSql, new { Sku = sku, Name = sku, By = by }, tx);
                    }

                    // Update existing warehouse stock
                    const string updStockSql = @"
UPDATE dbo.ItemWarehouseStock
   SET OnHand     = OnHand + @QtyDelta,
       Available  = CAST(CASE WHEN (OnHand + @QtyDelta - Reserved) < 0 
                              THEN 0 
                              ELSE (OnHand + @QtyDelta - Reserved) END AS INT),
       StrategyId = COALESCE(@StrategyId, StrategyId),
       BatchFlag  = @BatchFlag,
       SerialFlag = @SerialFlag
 WHERE ItemId = @ItemId AND WarehouseId = @WarehouseId 
   AND (BinId = @BinId OR (@BinId IS NULL AND BinId IS NULL));";

                    var affected = await dbConn.ExecuteAsync(
                        updStockSql,
                        new
                        {
                            ItemId = itemId.Value,
                            WarehouseId = ln.WarehouseId,
                            BinId = ln.BinId,
                            QtyDelta = ln.QtyDelta,
                            StrategyId = ln.StrategyId,
                            ln.BatchFlag,
                            ln.SerialFlag
                        },
                        tx
                    );

                    if (affected == 0)
                    {
                        const string insStockSql = @"
INSERT INTO dbo.ItemWarehouseStock
 (ItemId, WarehouseId, BinId, StrategyId, OnHand, Reserved, MinQty, MaxQty, ReorderQty, LeadTimeDays,
  BatchFlag, SerialFlag, Available, IsApproved, IsTransfered, StockIssueID, IsFullTransfer, IsPartialTransfer)
VALUES
 (@ItemId, @WarehouseId, @BinId, @StrategyId, @OnHand, 0, NULL, NULL, NULL, NULL,
  @BatchFlag, @SerialFlag, @Available, 0, 0, 0, 0, 0);";

                        decimal onHand = ln.QtyDelta;
                        int available = (int)Math.Max(0m, onHand);

                        await dbConn.ExecuteAsync(
                            insStockSql,
                            new
                            {
                                ItemId = itemId.Value,
                                WarehouseId = ln.WarehouseId,
                                BinId = ln.BinId,
                                StrategyId = ln.StrategyId,
                                OnHand = onHand,
                                ln.BatchFlag,
                                ln.SerialFlag,
                                Available = available
                            },
                            tx
                        );
                    }

                    // Upsert ItemPrice per (ItemId, SupplierId, WarehouseId) — PRICE accumulates
                    if (ln.SupplierId > 0 && ln.Price > 0)
                    {
                        await dbConn.ExecuteAsync(
                            UpsertItemPriceMergeSql,
                            new
                            {
                                ItemId = itemId.Value,
                                SupplierId = ln.SupplierId,
                                WarehouseId = ln.WarehouseId,
                                Price = ln.Price,                 // will be added to existing
                                Qty = (decimal?)ln.QtyDelta,      // add to Qty if provided
                                Barcode = ln.Barcode,
                                By = by
                            },
                            tx
                        );

                        // if needed later:
                        // await SnapshotBomIfChangedAsync(dbConn, tx, itemId.Value, ln.SupplierId, ln.Price, by);
                    }
                }

                tx.Commit();
            }
            catch
            {
                try { tx.Rollback(); } catch { /* ignore */ }
                throw;
            }
        }





        public async Task UpdateWarehouseAndSupplierPriceAsync(UpdateWarehouseSupplierPriceDto dto)
        {
            if (dto is null || string.IsNullOrWhiteSpace(dto.ItemCode))
                throw new ArgumentException("Invalid request.");

            if (!dto.SupplierId.HasValue || !dto.Price.HasValue)
                throw new ArgumentException("Supplier and Price are required to update cost.");

            var sku = dto.ItemCode.Trim();
            var by = dto.UpdatedBy ?? "";

            // Ensure ItemMaster exists
            const string findItemSql = @"SELECT Id FROM dbo.ItemMaster WHERE Sku = @Sku;";
            var itemId = await Connection.QueryFirstOrDefaultAsync<long?>(findItemSql, new { Sku = sku });

            if (itemId is null)
            {
                const string insertItemSql = @"
INSERT INTO dbo.ItemMaster
    (Sku, Name, Category, Uom, CostingMethodId, TaxCodeId, Specs, PictureUrl, IsActive,
     CreatedBy, CreatedDate, UpdatedBy, UpdatedDate)
OUTPUT INSERTED.Id
VALUES
    (@Sku, @Name, N'Uncategorized', N'EA', NULL, NULL, NULL, NULL, 1,
     @By, SYSUTCDATETIME(), @By, SYSUTCDATETIME());";

                itemId = await Connection.QueryFirstAsync<long>(insertItemSql, new { Sku = sku, Name = sku, By = by });
            }

            // Audit snapshot (old)
            var oldJson = await GetItemSnapshotJsonAsync(itemId.Value);

            var dbConn = (System.Data.Common.DbConnection)Connection;
            if (dbConn.State != System.Data.ConnectionState.Open)
                await dbConn.OpenAsync();

            using var tx = dbConn.BeginTransaction();

            try
            {
                // In UpdateWarehouseAndSupplierPriceAsync, replace the UPDATE with:
                const string updatePriceSql = @"
UPDATE dbo.ItemPrice
   SET
       -- PRICE: do NOT change existing price
       -- Price       = Price,
       Qty         = CASE WHEN @Qty IS NULL THEN Qty
                          ELSE ISNULL(Qty, 0) + @Qty END,
       Barcode     = COALESCE(@Barcode, Barcode),
       UpdatedBy   = @By,
       UpdatedDate = SYSUTCDATETIME()
 WHERE ItemId = @ItemId AND SupplierId = @SupplierId AND WarehouseId = @WarehouseId;";

                var affected = await dbConn.ExecuteAsync(
                    updatePriceSql,
                    new
                    {
                        ItemId = itemId.Value,
                        SupplierId = dto.SupplierId!.Value,
                        WarehouseId = dto.WarehouseId,
                        Price = dto.Price!.Value,
                        Qty = (decimal?)null,
                        Barcode = dto.Barcode,
                        By = by
                    },
                    tx
                );

                if (affected == 0)
                {
                    const string insertPriceSql = @"
INSERT INTO dbo.ItemPrice
    (ItemId, SupplierId, WarehouseId, Price, Qty, Barcode,
     CreatedBy, CreatedDate, UpdatedBy, UpdatedDate)
VALUES
    (@ItemId, @SupplierId, @WarehouseId, @Price, @Qty, @Barcode,
     @By, SYSUTCDATETIME(), @By, SYSUTCDATETIME());";

                    await dbConn.ExecuteAsync(
                        insertPriceSql,
                        new
                        {
                            ItemId = itemId.Value,
                            SupplierId = dto.SupplierId!.Value,
                            WarehouseId = dto.WarehouseId,
                            Price = dto.Price!.Value,
                            Qty = dto.QtyDelta,
                            Barcode = dto.Barcode,
                            By = by
                        },
                        tx
                    );
                }

                // BOM snapshot only when cost actually changed (rounded to 4)
                const string getLastBomSql = @"
SELECT TOP(1) UnitCost
FROM dbo.ItemBom
WHERE ItemId = @ItemId AND SupplierId = @SupplierId
ORDER BY Id DESC;";

                var lastUnitCost = await dbConn.ExecuteScalarAsync<decimal?>(
                    getLastBomSql,
                    new { ItemId = itemId.Value, SupplierId = dto.SupplierId!.Value },
                    tx
                );

                var newRounded = Math.Round(dto.Price!.Value, 4);
                var lastRounded = lastUnitCost.HasValue ? Math.Round(lastUnitCost.Value, 4) : (decimal?)null;

                if (lastRounded != newRounded)
                {
                    const string insertBomSql = @"
INSERT INTO dbo.ItemBom
    (ItemId, SupplierId, ExistingCost, UnitCost,
     CreatedBy, CreatedDate, UpdatedBy, UpdatedDate)
VALUES
    (@ItemId, @SupplierId, @ExistingCost, @UnitCost,
     @By, SYSUTCDATETIME(), @By, SYSUTCDATETIME());";

                    await dbConn.ExecuteAsync(
                        insertBomSql,
                        new
                        {
                            ItemId = itemId.Value,
                            SupplierId = dto.SupplierId!.Value,
                            ExistingCost = lastRounded ?? 0m,
                            UnitCost = newRounded,
                            By = by
                        },
                        tx
                    );
                }

                tx.Commit();
            }
            catch
            {
                try { tx.Rollback(); } catch { /* ignore rollback failure */ }
                throw;
            }

            // Audit after commit
            long? userId = long.TryParse(dto.UpdatedBy, out var uid) ? uid : (long?)null;
            var newJson = await GetItemSnapshotJsonAsync(itemId.Value);
            await AddAuditAsync(itemId.Value, "UPDATE", userId, oldJson, newJson, dto.Remarks);
        }


        // ==============================
        // At class level (once)
        // ==============================
        // Replace your UpsertItemPriceMergeSql with this
        private static readonly string UpsertItemPriceMergeSql = @"
MERGE dbo.ItemPrice WITH (HOLDLOCK) AS tgt
USING (
    SELECT
        @ItemId      AS ItemId,
        @SupplierId  AS SupplierId,
        @WarehouseId AS WarehouseId
) AS src
   ON tgt.ItemId      = src.ItemId
  AND tgt.SupplierId  = src.SupplierId
  AND tgt.WarehouseId = src.WarehouseId
WHEN MATCHED THEN
    UPDATE SET
        Qty         = CASE WHEN @Qty IS NULL THEN tgt.Qty
                           ELSE ISNULL(tgt.Qty, 0) + @Qty END,
        Barcode     = COALESCE(@Barcode, tgt.Barcode),
        UpdatedBy   = @By,
        UpdatedDate = SYSUTCDATETIME()

WHEN NOT MATCHED THEN
    INSERT (ItemId, SupplierId, WarehouseId, Price, Qty, Barcode, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate)
    VALUES (@ItemId, @SupplierId, @WarehouseId, @Price, COALESCE(@Qty, 0), @Barcode, @By, SYSUTCDATETIME(), @By, SYSUTCDATETIME());";







        // ===================== Helpers =====================

        private async Task<string> GetItemSnapshotJsonAsync(long id)
        {
            const string sql = @"
WITH base AS (
  SELECT 
    i.Id, i.Sku, i.Name, i.Category, i.Uom,
    i.CostingMethodId, i.TaxCodeId, i.Specs, i.PictureUrl,
    i.IsActive, i.CreatedBy, i.CreatedDate, i.UpdatedBy, i.UpdatedDate, i.ExpiryDate,

    Prices = ISNULL(JSON_QUERY((
      SELECT p.SupplierId, s.Name AS SupplierName, p.Price, p.Qty, p.Barcode   -- Qty included
      FROM dbo.ItemPrice p
      LEFT JOIN dbo.Suppliers s ON s.Id = p.SupplierId
      WHERE p.ItemId = i.Id
      FOR JSON PATH, INCLUDE_NULL_VALUES
    )), N'[]'),

    ItemStocks = ISNULL(JSON_QUERY((
      SELECT w.WarehouseId, w.BinId, w.StrategyId, w.OnHand, w.Reserved,
             CASE WHEN w.OnHand - w.Reserved < 0 THEN 0 ELSE w.OnHand - w.Reserved END AS Available,
             w.MinQty, w.MaxQty, w.ReorderQty, w.LeadTimeDays, w.BatchFlag, w.SerialFlag,
             w.IsApproved, w.IsTransfered, w.StockIssueID, w.IsFullTransfer, w.IsPartialTransfer
      FROM dbo.ItemWarehouseStock w
      WHERE w.ItemId = i.Id
      FOR JSON PATH, INCLUDE_NULL_VALUES
    )), N'[]'),

    BomLines = ISNULL(JSON_QUERY((   -- (unchanged)
      SELECT b.SupplierId, s.Name AS SupplierName, b.ExistingCost, b.UnitCost, b.CreatedBy
      FROM dbo.ItemBom b
      LEFT JOIN dbo.Suppliers s ON s.Id = b.SupplierId
      WHERE b.ItemId = i.Id
      FOR JSON PATH, INCLUDE_NULL_VALUES
    )), N'[]'),

    RolledUpCost = (
      SELECT SUM(ISNULL(b.UnitCost, 0.0))
      FROM dbo.ItemBom b
      WHERE b.ItemId = i.Id
    )
  FROM dbo.ItemMaster i
  WHERE i.Id = @Id
)
SELECT *
FROM base
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES;";
            var json = await Connection.QueryFirstOrDefaultAsync<string>(sql, new { Id = id });
            return json ?? "{}";
        }

        private Task AddAuditAsync(long itemId, string action, long? userId, string? oldJson, string? newJson, string? remarks = null)
        {
            const string insAudit = @"
INSERT INTO dbo.ItemMasterAudit (ItemId, Action, UserId, OldValuesJson, NewValuesJson, Remarks)
VALUES (@ItemId, @Action, @UserId, @OldValuesJson, @NewValuesJson, @Remarks);";
            return Connection.ExecuteAsync(insAudit, new
            {
                ItemId = itemId,
                Action = action,
                UserId = userId,
                OldValuesJson = oldJson,
                NewValuesJson = newJson,
                Remarks = remarks
            });
        }

        public async Task DeactivateAsync(int id)
        {
            const string sql = @"UPDATE dbo.ItemMaster SET IsActive = 0, UpdatedDate = SYSUTCDATETIME() WHERE Id = @Id;";
            await Connection.ExecuteAsync(sql, new { Id = id });
        }

        public async Task<IEnumerable<ItemWarehouseStockDTO>> GetAuditsByItemAsync(int itemId)
        {
            const string sql = @"
SELECT a.AuditId, a.ItemId, a.Action, a.OccurredAtUtc, a.UserId, u.UserName,
       a.OldValuesJson, a.NewValuesJson, a.Remarks
FROM dbo.ItemMasterAudit AS a
LEFT JOIN dbo.[User] AS u ON u.Id = a.UserId
WHERE a.ItemId = @ItemId
ORDER BY a.OccurredAtUtc DESC, a.AuditId DESC;";
            return await Connection.QueryAsync<ItemWarehouseStockDTO>(sql, new { ItemId = itemId });
        }

        public async Task<IEnumerable<ItemWarehouseStockDTO>> GetWarehouseStockByItemAsync(int itemId)
        {
            const string sql = @"
SELECT iws.Id, iws.ItemId, iws.WarehouseId, w.Name AS WarehouseName,
       iws.BinId, b.BinName, iws.StrategyId, iws.OnHand, iws.Reserved,
       iws.MinQty, iws.MaxQty, iws.ReorderQty, iws.LeadTimeDays,
       iws.BatchFlag, iws.SerialFlag, iws.Available
FROM dbo.ItemWarehouseStock iws
LEFT JOIN dbo.Warehouse w ON w.Id = iws.WarehouseId
LEFT JOIN dbo.Bin b       ON b.Id = iws.BinId
WHERE iws.ItemId = @ItemId
ORDER BY w.Name, b.BinName";
            return await Connection.QueryAsync<ItemWarehouseStockDTO>(sql, new { ItemId = itemId });
        }

        public async Task<IEnumerable<ItemWarehouseStockDTO>> GetSupplierPricesByItemAsync(int itemId)
        {
            const string sql = @"
SELECT ip.Id, ip.ItemId, ip.SupplierId, ip.Barcode, s.Name AS SupplierName, ip.Price, ip.Qty,ip.WarehouseId  -- Qty included
FROM dbo.ItemPrice ip
LEFT JOIN dbo.Suppliers s ON s.Id = ip.SupplierId
WHERE ip.ItemId = @ItemId
ORDER BY s.Name";
            return await Connection.QueryAsync<ItemWarehouseStockDTO>(sql, new { ItemId = itemId });
        }

        public async Task<BomSnapshot> GetBomSnapshotAsync(long itemId)
        {
            const string sql = @"
;WITH Ranked AS (
  SELECT b.*,
         ROW_NUMBER() OVER (
           PARTITION BY b.ItemId, b.SupplierId
           ORDER BY b.CreatedDate DESC, b.Id DESC
         ) AS rn
  FROM dbo.ItemBom b
  WHERE b.ItemId = @ItemId
)
SELECT
    r.SupplierId,
    s.Name AS SupplierName,
    r.ExistingCost,
    r.UnitCost,
    r.CreatedDate
FROM Ranked r
LEFT JOIN dbo.Suppliers s ON s.Id = r.SupplierId
WHERE r.rn = 1
ORDER BY r.SupplierId;

;WITH Ranked3 AS (
  SELECT b.*,
         ROW_NUMBER() OVER (
           PARTITION BY b.ItemId, b.SupplierId
           ORDER BY b.CreatedDate DESC, b.Id DESC
         ) AS rn
  FROM dbo.ItemBom b
  WHERE b.ItemId = @ItemId
)
SELECT
    r3.SupplierId,
    s2.Name AS SupplierName,
    r3.ExistingCost,
    r3.UnitCost,
    r3.CreatedDate,
    r3.rn
FROM Ranked3 r3
LEFT JOIN dbo.Suppliers s2 ON s2.Id = r3.SupplierId
WHERE r3.rn <= 3
ORDER BY r3.SupplierId, r3.rn;";

            using var multi = await Connection.QueryMultipleAsync(sql, new { ItemId = itemId });
            var latest = (await multi.ReadAsync<BomLatestRow>()).ToList();
            var history = (await multi.ReadAsync<BomHistoryPoint>()).ToList();
            return new BomSnapshot { Latest = latest, History = history };
        }
        public async Task<IEnumerable<StockAdjustmentItemsDTO?>> GetItemDetailsByItemId(int id)

        {

            const string sql = @"

select 

im.Name,im.Sku,im.Id as ItemId,

iws.Available,

bin.BinName,bin.ID as BinId,

ips.Qty,ips.Price,

wh.Name as WarehouseName, wh.Id as WarehouseId,

sp.Name as SupplierName, sp.Id as SupplierId 

from itemMaster as im

inner join ItemWarehouseStock as iws on iws.ItemId = im.Id

inner join Bin on bin.ID = iws.BinId

inner join ItemPrice as ips on ips.ItemId = im.Id

inner join Warehouse as wh on wh.Id = iws.id

inner join Suppliers as sp on sp.Id = ips.SupplierId

where im.Id = @Id";

            return await Connection.QueryAsync<StockAdjustmentItemsDTO>(sql, new { Id = id });

        }

    }
}
