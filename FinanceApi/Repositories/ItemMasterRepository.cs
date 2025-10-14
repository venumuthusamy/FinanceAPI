using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Repositories
{
    public class ItemMasterRepository : DynamicRepository, IItemMasterRepository
    {
        public ItemMasterRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

        public async Task<IEnumerable<ItemMasterDTO>> GetAllAsync()
        {
            const string sql = @"
SELECT i.*,
       ISNULL(inv.OnHand,0)   AS OnHand,
       ISNULL(inv.Reserved,0) AS Reserved,
       ISNULL(inv.OnHand,0) - ISNULL(inv.Reserved,0) AS Available
FROM ItemMaster i
LEFT JOIN InventorySummaries inv ON inv.ItemId = i.Id
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
FROM ItemMaster i
LEFT JOIN InventorySummaries inv ON inv.ItemId = i.Id
WHERE i.Id = @Id;";
            return await Connection.QueryFirstOrDefaultAsync<ItemMasterDTO>(sql, new { Id = id });
        }

        public async Task<int> CreateAsync(ItemMaster item)
        {
            item.CreatedDate = DateTime.UtcNow;
            item.UpdatedDate = DateTime.UtcNow;
            item.IsActive = true;

            const string sql = @"
INSERT INTO ItemMaster
 (Sku,Name,Category,Uom,Barcode,WareHouse,Costing,MinQty,MaxQty,ReorderQty,LeadTimeDays,BatchFlag,SerialFlag,TaxClass,Specs,PictureUrl,IsActive,CreatedDate,UpdatedDate)
OUTPUT INSERTED.Id
VALUES
 (@Sku,@Name,@Category,@Uom,@Barcode,@WareHouse,@Costing,@MinQty,@MaxQty,@ReorderQty,@LeadTimeDays,@BatchFlag,@SerialFlag,@TaxClass,@Specs,@PictureUrl,@IsActive,@CreatedDate,@UpdatedDate);";

            return await Connection.ExecuteScalarAsync<int>(sql, new
            {
                item.Sku,
                item.Name,
                item.Category,
                item.Uom,
                item.Barcode,
                item.Costing,
                item.WareHouse,
                MinQty = item.MinQty,
                MaxQty = item.MaxQty,
                ReorderQty = item.ReorderQty,
                LeadTimeDays = item.LeadTimeDays,
                item.BatchFlag,
                item.SerialFlag,
                TaxClass = item.TaxClass,
                item.Specs,
                item.PictureUrl,
                item.IsActive,
                item.CreatedDate,
                item.UpdatedDate
            });
        }

        public async Task UpdateAsync(ItemMaster item)
        {
            item.UpdatedDate = DateTime.UtcNow;

            const string sql = @"
UPDATE ItemMaster SET
 Sku=@Sku, Name=@Name, Category=@Category, Uom=@Uom, Barcode=@Barcode, Costing=@Costing,
 MinQty=@MinQty, MaxQty=@MaxQty, ReorderQty=@ReorderQty, LeadTimeDays=@LeadTimeDays,
 BatchFlag=@BatchFlag, SerialFlag=@SerialFlag, TaxClass=@TaxClass, Specs=@Specs, PictureUrl=@PictureUrl,
 UpdatedDate=@UpdatedDate
WHERE Id=@Id;";

            await Connection.ExecuteAsync(sql, new
            {
                item.Id,
                item.Sku,
                item.Name,
                item.Category,
                item.Uom,
                item.Barcode,
                item.Costing,
                MinQty = item.MinQty,
                MaxQty = item.MaxQty,
                ReorderQty = item.ReorderQty,
                LeadTimeDays = item.LeadTimeDays,
                item.BatchFlag,
                item.SerialFlag,
                TaxClass = item.TaxClass,
                item.Specs,
                item.PictureUrl,
                item.UpdatedDate
            });
        }

        public async Task DeactivateAsync(int id)
        {
            const string sql = "UPDATE ItemMaster SET IsActive = 0, UpdatedDate = SYSUTCDATETIME() WHERE Id = @Id;";
            await Connection.ExecuteAsync(sql, new { Id = id });
        }
    }
}
