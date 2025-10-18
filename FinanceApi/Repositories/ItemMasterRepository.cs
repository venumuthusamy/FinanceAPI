using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Interfaces;
using Microsoft.AspNetCore.Connections;
using System.Data;

namespace FinanceApi.Repositories
{
    // Same style as ApprovalLevelRepository: use base.Connection and Dapper only.
    public class ItemMasterRepository : DynamicRepository, IItemMasterRepository
    {
        public ItemMasterRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        // ===================== READS =====================

        public async Task<IEnumerable<ItemMasterDTO>> GetAllAsync()
        {
            const string sql = @"
SELECT i.*,
       ISNULL(inv.OnHand,0)   AS OnHand,
       ISNULL(inv.Reserved,0) AS Reserved,
       ISNULL(inv.OnHand,0) - ISNULL(inv.Reserved,0) AS Available
FROM dbo.ItemMaster i
LEFT JOIN dbo.InventorySummaries inv ON inv.ItemId = i.Id
WHERE i.IsActive = 1
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

        // Create only ItemMaster row (UOM-style, single table)
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

        // ===== Overloads to handle child tables (ItemPrice, ItemWarehouse) =====
        // Use this overload when the API receives a DTO with children from the UI.
        public async Task<long> CreateAsync(ItemMasterUpsertDto dto)
        {
            // 1) Insert parent
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

            // 2) Insert prices (if any)
            if (dto.Prices is not null && dto.Prices.Count > 0)
            {
                const string ip = @"INSERT INTO dbo.ItemPrice (ItemId,SupplierId,Price,Barcode) VALUES (@ItemId,@SupplierId,@Price,@Barcode);";
                foreach (var p in dto.Prices)
                {
                    await Connection.ExecuteAsync(ip, new
                    {
                        ItemId = itemId,
                        SupplierId = p.SupplierId,
                        Price = p.Price,
                        Barcode = p.Barcode
                    });
                }
            }

            // 3) Insert warehouses (if any)
            if (dto.ItemStocks is not null && dto.ItemStocks.Count > 0)
            {
                const string iw = @"
INSERT INTO dbo.ItemWarehouseStock
 (ItemId,WarehouseId,BinId,StrategyId,OnHand,Reserved,MinQty,MaxQty,ReorderQty,LeadTimeDays,BatchFlag,SerialFlag,Available,IsApproved,IsTransfered,StockIssueID)
VALUES(@ItemId,@WarehouseId,@BinId,@StrategyId,@OnHand,@Reserved,@MinQty,@MaxQty,@ReorderQty,@LeadTimeDays,@BatchFlag,@SerialFlag,@Available,@IsApproved,@IsTransfered,@StockIssueID);";
                foreach (var s in dto.ItemStocks)
                {
                    await Connection.ExecuteAsync(iw, new
                    {
                        ItemId = itemId,
                        s.WarehouseId,
                        s.BinId,
                        s.StrategyId,
                        s.OnHand,
                        s.Reserved,
                        s.MinQty,
                        s.MaxQty,
                        s.ReorderQty,
                        s.LeadTimeDays,
                        s.BatchFlag,
                        s.SerialFlag,
                        s.Available,
                        s.IsApproved,
                        s.IsTransfered,
                        s.StockIssueID
                    });
                }
            }

            // 4) 🟢 CREATE audit (no triggers)
            long? userId = null;
            if (long.TryParse(dto.CreatedBy, out var uid)) userId = uid;

            var newJson = await GetItemSnapshotJsonAsync(itemId);      // AFTER create snapshot
            await AddAuditAsync(itemId, "CREATE", userId, null, newJson, null);

            return itemId;
        }


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

        // Overload that replaces children, matching your UI DTO shape.
        public async Task UpdateAsync(ItemMasterUpsertDto dto)
        {
            // 0) snapshot BEFORE update
            var oldJson = await GetItemSnapshotJsonAsync(dto.Id);

            // 1) Update parent
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

            // 2) Replace children (your existing logic)
            await Connection.ExecuteAsync("DELETE FROM dbo.ItemPrice WHERE ItemId=@Id;", new { dto.Id });
            await Connection.ExecuteAsync("DELETE FROM dbo.ItemWarehouseStock WHERE ItemId=@Id;", new { dto.Id });

            if (dto.Prices is not null && dto.Prices.Count > 0)
            {
                const string ip = @"INSERT INTO dbo.ItemPrice (ItemId,SupplierId,Price,Barcode) VALUES (@ItemId,@SupplierId,@Price,@Barcode);";
                foreach (var p in dto.Prices)
                {
                    await Connection.ExecuteAsync(ip, new
                    {
                        ItemId = dto.Id,
                        SupplierId = p.SupplierId,
                        Price = p.Price,
                        Barcode=p.Barcode
                    });
                }
            }

            if (dto.ItemStocks is not null && dto.ItemStocks.Count > 0)
            {
                const string iw = @"
INSERT INTO dbo.ItemWarehouseStock
 (ItemId,WarehouseId,BinId,StrategyId,OnHand,Reserved,MinQty,MaxQty,ReorderQty,LeadTimeDays,BatchFlag,SerialFlag,Available,IsApproved,IsTransfered,StockIssueID)
VALUES(@ItemId,@WarehouseId,@BinId,@StrategyId,@OnHand,@Reserved,@MinQty,@MaxQty,@ReorderQty,@LeadTimeDays,@BatchFlag,@SerialFlag,@Available,@IsApproved,@IsTransfered,@StockIssueID);";
                foreach (var s in dto.ItemStocks)
                {
                    await Connection.ExecuteAsync(iw, new
                    {
                        ItemId = dto.Id,
                        s.WarehouseId,
                        s.BinId,
                        s.StrategyId,
                        s.OnHand,
                        s.Reserved,
                        s.MinQty,
                        s.MaxQty,
                        s.ReorderQty,
                        s.LeadTimeDays,
                        s.BatchFlag,
                        s.SerialFlag,
                        s.Available,
                        s.IsApproved,
                        s.IsTransfered,
                        s.StockIssueID
                    });
                }
            }

            // 3) 🟡 UPDATE audit (diff-friendly: before & after)
            long? userId = null;
            if (long.TryParse(dto.UpdatedBy, out var uid)) userId = uid;

            var newJson = await GetItemSnapshotJsonAsync(dto.Id);      // AFTER update snapshot
            await AddAuditAsync(dto.Id, "UPDATE", userId, oldJson, newJson, null);
        }


        public async Task DeactivateAsync(int id)
        {
            const string sql = @"UPDATE dbo.ItemMaster SET IsActive = 0, UpdatedDate = SYSUTCDATETIME() WHERE Id = @Id;";
            await Connection.ExecuteAsync(sql, new { Id = id });
        }
        private Task<string?> GetItemSnapshotJsonAsync(long id)
        {
            const string sql = @"
SELECT i.*
FROM dbo.ItemMaster i
WHERE i.Id = @Id
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES;";
            return Connection.QueryFirstOrDefaultAsync<string>(sql, new { Id = id });
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
        public async Task<IEnumerable<ItemWarehouseStockDTO>> GetAuditsByItemAsync(int itemId)
        {
            const string sql = @"
SELECT
    a.AuditId,
    a.ItemId,
    a.Action,
    a.OccurredAtUtc,
    a.UserId,
    u.UserName,
    a.OldValuesJson,
    a.NewValuesJson,
    a.Remarks
FROM dbo.ItemMasterAudit AS a
LEFT JOIN dbo.[User]     AS u ON u.Id = a.UserId
WHERE a.ItemId = @ItemId
ORDER BY a.OccurredAtUtc DESC, a.AuditId DESC;";

            return await Connection.QueryAsync<ItemWarehouseStockDTO>(sql, new { ItemId = itemId });
        }
        public async Task<IEnumerable<ItemWarehouseStockDTO>> GetWarehouseStockByItemAsync(int itemId)
        {
            const string sql = @"
SELECT 
    iws.Id,
    iws.ItemId,
    iws.WarehouseId,
    w.Name       AS WarehouseName,
    iws.BinId,
    b.BinName,
    iws.StrategyId,
    iws.OnHand,
    iws.Reserved,
    iws.MinQty,
    iws.MaxQty,
    iws.ReorderQty,
    iws.LeadTimeDays,
    iws.BatchFlag,
    iws.SerialFlag,
   
    iws.Available
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
SELECT 
    ip.Id,
    ip.ItemId,
    ip.SupplierId,
ip.Barcode,
    s.Name AS SupplierName,
    ip.Price
FROM dbo.ItemPrice ip
LEFT JOIN dbo.Suppliers s ON s.Id = ip.SupplierId
WHERE ip.ItemId = @ItemId
ORDER BY s.Name";
           
            return await Connection.QueryAsync<ItemWarehouseStockDTO>(sql, new { ItemId = itemId });
        }

    }
}
