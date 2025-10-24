using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using System.Data;
using System.Data.Common;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace FinanceApi.Repositories
{
    public class StockRepository : DynamicRepository,IStockRepository
    {

        public StockRepository(IDbConnectionFactory connectionFactory)
     : base(connectionFactory)
        {
        }

        public async Task<IEnumerable<StockDTO>> GetAllAsync()
        {
            const string query = @"
                SELECT * from Stock";

            return await Connection.QueryAsync<StockDTO>(query);
        }


        public async Task<StockDTO> GetByIdAsync(long id)
        {

            const string query = "SELECT * FROM Stock WHERE Id = @Id";

            return await Connection.QuerySingleAsync<StockDTO>(query, new { Id = id });
        }

        public async Task<int> InsertBulkAsync(IEnumerable<Stock> stocks)
        {
            const string query = @"
INSERT INTO [Finance].[dbo].[Stock] (
    ItemID,
    FromWarehouseID,
    ToWarehouseID,
    Available,
    OnHand,
    Reserved,
    Min,
    Expiry,
    isApproved,
    CreatedBy,
    CreatedDate,
    UpdatedBy,
    UpdatedDate,
    FromWarehouseName,
    ItemName,
    Sku,
    BinId,
    BinName,
Remarks
)
VALUES (
    @ItemId,
    @FromWarehouseID,
    @ToWarehouseID,
    @Available,
    @OnHand,
    @Reserved,
    @Min,
    @Expiry,
    @IsApproved,
    @CreatedBy,
    @CreatedDate,
    @UpdatedBy,
    @UpdatedDate,
    @FromWarehouseName,
    @ItemName,
    @Sku,
    @BinId,
    @BinName,
@Remarks
);";

            // ExecuteAsync returns total affected rows
            return await Connection.ExecuteAsync(query, stocks);
        }




        public async Task UpdateAsync(Stock stock)
        {
            const string query = "UPDATE Stock SET OnHand = @OnHand,Available =@Available WHERE Id = @Id";
            await Connection.ExecuteAsync(query, stock);
        }

        public async Task DeactivateAsync(int id)
        {
            const string query = "UPDATE Stock SET IsActive = 0 WHERE ID = @id";
            await Connection.ExecuteAsync(query, new { ID = id });
        }


        public async Task<IEnumerable<StockListViewInfo>> GetAllStockList()
        {
            const string query = @"
                                                   select im.Id,im.Name,im.Sku,
iws.WarehouseId,wh.Name as WarehouseName,
iws.BinId,bn.BinName,
iws.Available,
iws.OnHand,iws.MinQty,iws.MaxQty,iws.Reserved,im.ExpiryDate,im.Category,im.Uom
from ItemMaster as im
inner join ItemWarehouseStock as iws on iws.ItemId = im.Id
inner join Warehouse as wh on wh.Id = iws.WarehouseId
inner join BIN as bn on bn.ID = iws.BinId
where iws.Available != 0 and iws.IsTransfered = 0 ;
";

            return await Connection.QueryAsync<StockListViewInfo>(query);
        }


        public async Task<IEnumerable<StockListViewInfo>> GetAllItemStockList()
        {
            const string query = @"
              select im.Id,im.Name,im.Sku,
iws.WarehouseId,wh.Name as WarehouseName,
iws.BinId,bn.BinName,
iws.Available,
iws.OnHand,iws.MinQty,iws.MaxQty,iws.Reserved,im.ExpiryDate,im.Category,im.Uom
from ItemMaster as im
inner join ItemWarehouseStock as iws on iws.ItemId = im.Id
inner join Warehouse as wh on wh.Id = iws.WarehouseId
inner join BIN as bn on bn.ID = iws.BinId;
";

            return await Connection.QueryAsync<StockListViewInfo>(query);
        }




        public async Task<int> MarkAsTransferredBulkAsync(IEnumerable<MarkAsTransferredRequest> requests)
        {
            const string query = @"
UPDATE [Finance].[dbo].[ItemWarehouseStock]
SET IsTransfered = 1
WHERE ItemId = @ItemId
  AND WarehouseId = @WarehouseId
  AND (BinId = @BinId OR (@BinId IS NULL AND BinId IS NULL));";

            // ExecuteAsync can handle IEnumerable<T> for multiple updates
            return await Connection.ExecuteAsync(query, requests);
        }


        public async Task<IEnumerable<StockTransferListViewInfo>> GetAllStockTransferedList()
        {
            const string query = @"
                                                   select
	s.Id AS StockId,          -- ✅ add this line
    im.Id AS ItemId,          -- ✅ rename for clarity
    im.Name,
    im.Sku,
    iws.WarehouseId,
    wh.Name AS WarehouseName,
    iws.BinId,
    bn.BinName,
    s.Available,
    s.IsApproved,
    iws.IsTransfered,
    s.OnHand,
    iws.MinQty,
    iws.MaxQty,
    s.Reserved,
    im.ExpiryDate,
    im.Category,
    im.Uom,
    s.TransferQty,
    s.FromWarehouseID,
    s.ToWarehouseID,
    whFrom.Name AS FromWarehouseName,
    whTo.Name AS ToWarehouseName,
    s.Remarks
FROM ItemMaster AS im
INNER JOIN ItemWarehouseStock AS iws 
    ON iws.ItemId = im.Id
INNER JOIN Warehouse AS wh 
    ON wh.Id = iws.WarehouseId
INNER JOIN BIN AS bn 
    ON bn.ID = iws.BinId
INNER JOIN Stock AS s 
    ON s.ItemId = iws.ItemId
LEFT JOIN Warehouse AS whFrom 
    ON whFrom.Id = s.FromWarehouseID
LEFT JOIN Warehouse AS whTo 
    ON whTo.Id = s.ToWarehouseID
WHERE iws.IsTransfered = 1;
";

            return await Connection.QueryAsync<StockTransferListViewInfo>(query);
        }


        public async Task<int> AdjustOnHandAsync(AdjustOnHandRequest request)
        {
            const string query = @"
UPDATE [dbo].[ItemWarehouseStock]
SET 
    OnHand = @NewOnHand,
    Available = CASE 
                  WHEN @NewOnHand - ISNULL(Reserved, 0) < 0 THEN 0 
                  ELSE @NewOnHand - ISNULL(Reserved, 0) 
                END,
    StockIssueID = @StockIssueID
WHERE ItemId = @ItemId
  AND WarehouseId = @WarehouseId
  AND (BinId = @BinId OR (@BinId IS NULL AND BinId IS NULL));";

            return await Connection.ExecuteAsync(query, request);
        }


        public async Task<ApproveBulkResult> ApproveTransfersBulkAsync(IEnumerable<ApproveTransferRequest> requests)
        {
            var result = new ApproveBulkResult();

            const string sqlIws = @"
UPDATE [Finance].[dbo].[ItemWarehouseStock]
SET 
    IsApproved        = 1,
    Available         = Available - @TransferQty,
    OnHand            = OnHand - @TransferQty,
    IsFullTransfer    = @IsFullTransfer,
    IsPartialTransfer = @IsPartialTransfer,
IsTransfered      = CASE 
                           WHEN @IsPartialTransfer = 1 THEN 0  -- partial → mark as not transferred
                           ELSE IsTransfered                   -- keep existing value
                        END,
    WarehouseId = CASE 
                    WHEN @IsFullTransfer = 1 THEN @ToWarehouseId  -- only change if full transfer
                    ELSE WarehouseId 
                  END
WHERE ItemId = @ItemId
  AND WarehouseId = @WarehouseId
  AND (BinId = @BinId OR (@BinId IS NULL AND BinId IS NULL));";

            // ✅ Change here: use Id for Stock update
            const string sqlStock = @"
UPDATE [Finance].[dbo].[Stock]
SET 
    ToWarehouseID     = @ToWarehouseId,
    IsApproved        = 1,
    Remarks           = COALESCE(@Remarks, Remarks),
    Available         = Available - @TransferQty,
    OnHand            = OnHand - @TransferQty,
    TransferQty       = @TransferQty
WHERE Id = @StockId;  -- use Id instead of ItemId + Warehouse filters
";

            var countIws = await Connection.ExecuteAsync(sqlIws, requests);
            var countStock = await Connection.ExecuteAsync(sqlStock, requests);

            result.UpdatedItemWarehouseStock = countIws;
            result.UpdatedStock = countStock;

            return result;
        }





        public async Task<StockHistoryViewInfo> GetByIdStockHistory(long id)
        {

            const string query = @"
SELECT 
s.ID,
    im.Id,
    im.Name,
    im.Sku,
    s.FromWarehouseID,
    whFrom.Name AS FromWarehouseName,
    s.ToWarehouseID,
    ISNULL(whTo.Name, 'Pending') AS ToWarehouseName,
    s.TransferQty,      -- transfer quantity
    s.Available       -- available quantity from warehouse stock
FROM ItemMaster AS im
INNER JOIN ItemWarehouseStock AS iws 
    ON iws.ItemId = im.Id
INNER JOIN Stock AS s 
    ON s.ItemId = iws.ItemId
LEFT JOIN Warehouse AS whFrom 
    ON whFrom.Id = s.FromWarehouseID
LEFT JOIN Warehouse AS whTo 
    ON whTo.Id = s.ToWarehouseID
WHERE iws.IsTransfered = 1 AND s.Id = @Id

";
            return await Connection.QuerySingleAsync<StockHistoryViewInfo>(query, new { Id = id });
        }



    }
}
