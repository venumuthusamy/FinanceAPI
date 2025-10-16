using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Interfaces;

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
                item.Barcode,
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
 (Sku,Name,Category,Uom,Barcode,CostingMethodId,TaxCodeId,Specs,PictureUrl,IsActive,CreatedBy,CreatedDate,UpdatedBy,UpdatedDate,ExpiryDate)
OUTPUT INSERTED.Id
VALUES(@Sku,@Name,@Category,@Uom,@Barcode,@CostingMethodId,@TaxCodeId,@Specs,@PictureUrl,1,@CreatedBy,SYSUTCDATETIME(),@UpdatedBy,SYSUTCDATETIME(),@ExpiryDate);";

            var itemId = await Connection.QueryFirstAsync<long>(ins, new
            {
                dto.Sku,
                dto.Name,
                dto.Category,
                dto.Uom,
                dto.Barcode,
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
                const string ip = @"INSERT INTO dbo.ItemPrice (ItemId,SupplierId,Price) VALUES (@ItemId,@SupplierId,@Price);";
                foreach (var p in dto.Prices)
                {
                    await Connection.ExecuteAsync(ip, new
                    {
                        ItemId = itemId,
                        SupplierId = p.SupplierId,
                        Price = p.Price
                    });
                }
            }

            // 3) Insert warehouses (if any)
            if (dto.ItemStocks is not null && dto.ItemStocks.Count > 0)
            {
                const string iw = @"
INSERT INTO dbo.ItemWarehouseStock
 (ItemId,WarehouseId,BinId,StrategyId,OnHand,Reserved,MinQty,MaxQty,ReorderQty,LeadTimeDays,BatchFlag,SerialFlag,Available)
VALUES(@ItemId,@WarehouseId,@BinId,@StrategyId,@OnHand,@Reserved,@MinQty,@MaxQty,@ReorderQty,@LeadTimeDays,@BatchFlag,@SerialFlag,@Available);";
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
                        s.Available
                    });
                }
            }

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
    Barcode=@Barcode,
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
                item.Barcode,
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
            const string up = @"
UPDATE dbo.ItemMaster SET
  Sku=@Sku, Name=@Name, Category=@Category, Uom=@Uom, Barcode=@Barcode,
  CostingMethodId=@CostingMethodId, TaxCodeId=@TaxCodeId, Specs=@Specs,
  PictureUrl=@PictureUrl, IsActive=@IsActive, UpdatedDate=SYSUTCDATETIME(),ExpiryDate=@ExpiryDate 
WHERE Id=@Id;";
            await Connection.ExecuteAsync(up, new
            {
                dto.Id,
                dto.Sku,
                dto.Name,
                dto.Category,
                dto.Uom,
                dto.Barcode,
                dto.CostingMethodId,
                dto.TaxCodeId,
                dto.Specs,
                dto.PictureUrl,
                dto.IsActive,
                dto.ExpiryDate
            });

            // Replace child rows (simple and safe, same style as your other repos)
            await Connection.ExecuteAsync("DELETE FROM dbo.ItemPrice WHERE ItemId=@Id;", new { dto.Id });
            await Connection.ExecuteAsync("DELETE FROM dbo.ItemWarehouse WHERE ItemId=@Id;", new { dto.Id });

            if (dto.Prices is not null && dto.Prices.Count > 0)
            {
                const string ip = @"INSERT INTO dbo.ItemPrice (ItemId,SupplierId,Price) VALUES (@ItemId,@SupplierId,@Price);";
                foreach (var p in dto.Prices)
                {
                    await Connection.ExecuteAsync(ip, new
                    {
                        ItemId = dto.Id,
                        SupplierId = p.SupplierId,
                        Price = p.Price
                    });
                }
            }

            if (dto.ItemStocks is not null && dto.ItemStocks.Count > 0)
            {
                const string iw = @"
INSERT INTO dbo.ItemWarehouseStock
 (ItemId,WarehouseId,BinId,StrategyId,OnHand,Reserved,MinQty,MaxQty,ReorderQty,LeadTimeDays,BatchFlag,SerialFlag,Available)
VALUES(@ItemId,@WarehouseId,@BinId,@StrategyId,@OnHand,@Reserved,@MinQty,@MaxQty,@ReorderQty,@LeadTimeDays,@BatchFlag,@SerialFlag,@Available);";
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
                        s.Available
                    });
                }
            }
        }

        public async Task DeactivateAsync(int id)
        {
            const string sql = @"UPDATE dbo.ItemMaster SET IsActive = 0, UpdatedDate = SYSUTCDATETIME() WHERE Id = @Id;";
            await Connection.ExecuteAsync(sql, new { Id = id });
        }
    }
}
