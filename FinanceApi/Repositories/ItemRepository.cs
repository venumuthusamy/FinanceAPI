﻿using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Repositories
{
    using Dapper;

    public class ItemRepository : DynamicRepository, IItemRepository
    {
        public ItemRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory) { }

        public async Task<IEnumerable<ItemDto>> GetAllAsync()
        {
            const string sql = @"
SELECT  i.Id,
        i.ItemCode,
        i.ItemName,
        i.UomId,
        COALESCE(u.Name,'')           AS UomName,
        i.BudgetLineId,
        COALESCE(coa.HeadName,'')     AS BudgetLineName,
		c.CatagoryName,
i.CategoryId,
        i.CreatedBy,
        i.CreatedDate,
        i.UpdatedBy,
        i.UpdatedDate,
        i.IsActive
FROM    Item i
LEFT JOIN Uom u            ON u.Id  = i.UomId
LEFT JOIN ChartOfAccount coa ON coa.Id = i.BudgetLineId
inner join Catagory as c on c.ID=i.CategoryId
WHERE   i.IsActive = 1
ORDER BY i.Id;";
            return await Connection.QueryAsync<ItemDto>(sql);
        }

        public async Task<ItemDto?> GetByIdAsync(int id)
        {
            const string sql = @"
SELECT  i.Id,
        i.ItemCode,
        i.ItemName,
        i.UomId,
        COALESCE(u.Name,'')           AS UomName,
        i.BudgetLineId,
        COALESCE(coa.HeadName,'')     AS BudgetLineName,
        i.CreatedBy,
        i.CreatedDate,
        i.UpdatedBy,
        i.UpdatedDate,
        i.IsActive
FROM    Item i
LEFT JOIN Uom u            ON u.Id  = i.UomId
LEFT JOIN ChartOfAccount coa ON coa.Id = i.BudgetLineId
WHERE   i.Id = @Id AND i.IsActive = 1;";
            return await Connection.QueryFirstOrDefaultAsync<ItemDto>(sql, new { Id = id });
        }

        public async Task<int> CreateAsync(Item item)
        {
            // Set audit defaults (like your EF repo did)
            item.CreatedBy = item.CreatedBy ?? "System";
            item.CreatedDate = DateTime.UtcNow;
            item.IsActive = true;

            const string sql = @"
INSERT INTO Item
    (ItemCode, ItemName,CategoryId, UomId, BudgetLineId, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive)
OUTPUT INSERTED.Id
VALUES
    (@ItemCode, @ItemName,@CategoryId, @UomId, @BudgetLineId, @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, @IsActive);";

            return await Connection.QueryFirstAsync<int>(sql, item);
        }

        public async Task UpdateAsync(Item item)
        {
            // Route/controller will set item.Id and audit
            const string sql = @"
UPDATE Item
SET ItemCode      = @ItemCode,
    ItemName      = @ItemName,
    CategoryId    = @CategoryId,
    UomId         = @UomId,
    BudgetLineId  = @BudgetLineId,
    UpdatedBy     = @UpdatedBy,
    UpdatedDate   = @UpdatedDate
WHERE Id = @Id;";

            await Connection.ExecuteAsync(sql, item);
        }

        public async Task DeactivateAsync(int id)
        {
            const string sql = @"UPDATE Item SET IsActive = 0 WHERE Id = @Id;";
            await Connection.ExecuteAsync(sql, new { Id = id });
        }

        public async Task<bool> ExistsInItemMasterAsync(string itemCode)
        {
            const string sql = @"
SELECT TOP(1) 1
FROM ItemMaster
WHERE LTRIM(RTRIM(Sku)) = LTRIM(RTRIM(@itemCode));";

            var exists = await Connection.ExecuteScalarAsync<int?>(sql, new { itemCode });
            return exists.HasValue;
        }
    }

}
