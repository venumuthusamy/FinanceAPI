using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.Data.SqlClient;
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
INSERT INTO [dbo].[Stock] (
    ItemID,
    FromWarehouseID,
    ToWarehouseID,
    Available,
    OnHand,
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
    Remarks,
    SupplierId,       
    IsSupplierBased,
ToBinId
)
VALUES (
    @ItemId,
    @FromWarehouseID,
    @ToWarehouseID,
    @Available,
    @OnHand,
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
    @Remarks,
    @SupplierId,       
    @IsSupplierBased,
@ToBinId
);";

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
WITH StockSummary AS (
    SELECT
        iws.ItemId,
        iws.WarehouseId,
        SUM(iws.OnHand) AS OnHand,
        SUM(iws.Available) AS Available,
        SUM(iws.Reserved) AS Reserved,
        MAX(iws.MinQty) AS MinQty,
        MAX(iws.MaxQty) AS MaxQty,
        MAX(bn.Id) AS BinId,               -- ✅ fixed here
        MAX(bn.BinName) AS BinName
    FROM ItemWarehouseStock iws
    INNER JOIN BIN bn ON bn.Id = iws.BinId -- ✅ fixed join column
    GROUP BY iws.ItemId, iws.WarehouseId
),
SupplierList AS (
    SELECT
        ip.ItemId,
        ip.WarehouseId,
        ip.SupplierId,
        s.Name AS SupplierName,
        ip.Qty,
        ROW_NUMBER() OVER(PARTITION BY ip.ItemId, ip.WarehouseId ORDER BY ip.SupplierId) AS rn
    FROM ItemPrice ip
    INNER JOIN Suppliers s ON s.Id = ip.SupplierId
    WHERE ip.IsTransfered = 0
)
SELECT
    im.Id,
    im.Name,
    im.Sku,
    wh.Id AS WarehouseId,
    wh.Name AS WarehouseName,
    ss.BinId,
    ss.BinName,
    sl.SupplierId,
    sl.SupplierName,
    ss.OnHand,
    CASE WHEN sl.rn = 1 THEN ss.Reserved ELSE 0 END AS Reserved,  -- ✅ Reserved only once per warehouse
    ss.Available,
    ss.MinQty,
    ss.MaxQty,
    im.ExpiryDate,
    im.Category,
    im.Uom,
    sl.Qty AS Qty
FROM SupplierList sl
INNER JOIN StockSummary ss 
    ON ss.ItemId = sl.ItemId 
    AND ss.WarehouseId = sl.WarehouseId
INNER JOIN ItemMaster im ON im.Id = sl.ItemId
INNER JOIN Warehouse wh ON wh.Id = sl.WarehouseId
ORDER BY im.Name, wh.Name, sl.SupplierName;

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
            const string updateIws = @"
UPDATE iws
SET    IsTransfered = 1
FROM   [dbo].[ItemWarehouseStock] iws
WHERE  iws.ItemId      = @ItemId
  AND  iws.WarehouseId = @WarehouseId
  AND (
        (iws.BinId IS NULL AND @BinId IS NULL)
        OR iws.BinId = @BinId
      );";

            const string updateItemPrice = @"
UPDATE ip
SET    IsTransfered = 1
FROM   [dbo].[ItemPrice] ip
WHERE  ip.ItemId      = @ItemId
  AND  ip.WarehouseId = @WarehouseId
  AND (
        (ip.SupplierId IS NULL AND @SupplierId IS NULL)
        OR ip.SupplierId = @SupplierId
      );";

            // ✅ Cast to SqlConnection for async support
            var sqlConn = (SqlConnection)Connection;

            if (sqlConn.State != ConnectionState.Open)
                await sqlConn.OpenAsync();

            using var tx = sqlConn.BeginTransaction();
            try
            {
                // Run both updates in a single transaction
                var a = await sqlConn.ExecuteAsync(updateIws, requests, tx);
                var b = await sqlConn.ExecuteAsync(updateItemPrice, requests, tx);

                tx.Commit();
                return a + b; // total rows affected
            }
            catch
            {
                tx.Rollback();
                throw;
            }
            finally
            {
                await sqlConn.CloseAsync();
            }
        }





        public async Task<IEnumerable<StockTransferListViewInfo>> GetAllStockTransferedList()
        {
            const string query = @"
WITH RS AS (
  SELECT
      iws.ItemId,
      iws.WarehouseId,
      SUM(ISNULL(iws.Reserved,0)) AS Reserved,
      MAX(ISNULL(iws.MinQty,0))   AS MinQty,
      MAX(ISNULL(iws.MaxQty,0))   AS MaxQty
  FROM ItemWarehouseStock iws
  GROUP BY iws.ItemId, iws.WarehouseId
)
SELECT
    s.Id                  AS StockId,
    im.Id                 AS ItemId,
    im.Name,
    im.Sku,

    s.FromWarehouseID     AS WarehouseId,
    whFrom.Name           AS WarehouseName,

    s.FromWarehouseID,
    whFrom.Name           AS FromWarehouseName,
    s.ToWarehouseID,
    whTo.Name             AS ToWarehouseName,

    s.BinId,
    bn.BinName,

    ISNULL(iws.OnHand,0)      AS OnHand,
    ISNULL(iws.Available,0)   AS Available,
    ISNULL(rs.Reserved,0)     AS Reserved,
    ISNULL(rs.MinQty,0)       AS MinQty,
    ISNULL(rs.MaxQty,0)       AS MaxQty,

    lp.SupplierId,
    lp.Price,
    sp.Name               AS SupplierName,

    s.TransferQty,
    s.Remarks,
    s.IsApproved,
    s.IsSupplierBased
FROM Stock s
JOIN ItemMaster im ON im.Id = s.ItemId

OUTER APPLY (
    SELECT TOP (1) ip.SupplierId, ip.Price
    FROM ItemPrice ip
    WHERE ip.ItemId = s.ItemId
      AND (ip.SupplierId = s.SupplierId OR s.SupplierId IS NULL)
      AND ip.WarehouseId = s.FromWarehouseID
    ORDER BY ip.Id DESC
) lp

LEFT JOIN Suppliers sp   ON sp.Id     = lp.SupplierId
LEFT JOIN Warehouse whFrom ON whFrom.Id = s.FromWarehouseID
LEFT JOIN Warehouse whTo   ON whTo.Id   = s.ToWarehouseID
LEFT JOIN Bin bn           ON bn.Id     = s.BinId

LEFT JOIN ItemWarehouseStock iws
       ON iws.ItemId = s.ItemId
      AND iws.WarehouseId = s.FromWarehouseID
      AND (iws.BinId = s.BinId OR s.BinId IS NULL)

LEFT JOIN RS rs
       ON rs.ItemId      = s.ItemId
      AND rs.WarehouseId = s.FromWarehouseID

WHERE s.isApproved = 1    
ORDER BY s.FromWarehouseID, lp.SupplierId;

";
            return await Connection.QueryAsync<StockTransferListViewInfo>(query);
        }


        public async Task<IEnumerable<StockTransferListViewInfo>> GetStockTransferedList()
        {
            const string query = @"
WITH RS AS (
  SELECT
      iws.ItemId,
      iws.WarehouseId,
      SUM(ISNULL(iws.Reserved,0)) AS Reserved,
      MAX(ISNULL(iws.MinQty,0))   AS MinQty,
      MAX(ISNULL(iws.MaxQty,0))   AS MaxQty
  FROM ItemWarehouseStock iws
  GROUP BY iws.ItemId, iws.WarehouseId
)
SELECT
    s.Id                  AS StockId,
    im.Id                 AS ItemId,
    im.Name,
    im.Sku,

    s.FromWarehouseID     AS WarehouseId,
    whFrom.Name           AS WarehouseName,

    s.FromWarehouseID,
    whFrom.Name           AS FromWarehouseName,
    s.ToWarehouseID,
    whTo.Name             AS ToWarehouseName,

    s.BinId,
    bn.BinName,

    ISNULL(iws.OnHand,0)      AS OnHand,
    ISNULL(iws.Available,0)   AS Available,
    ISNULL(rs.Reserved,0)     AS Reserved,
    ISNULL(rs.MinQty,0)       AS MinQty,
    ISNULL(rs.MaxQty,0)       AS MaxQty,

    lp.SupplierId,
    lp.Price,
    sp.Name               AS SupplierName,

    s.TransferQty,
    s.Remarks,
    s.IsApproved,
    s.IsSupplierBased
FROM Stock s
JOIN ItemMaster im ON im.Id = s.ItemId

OUTER APPLY (
    SELECT TOP (1) ip.SupplierId, ip.Price
    FROM ItemPrice ip
    WHERE ip.ItemId = s.ItemId
      AND (ip.SupplierId = s.SupplierId OR s.SupplierId IS NULL)
      AND ip.WarehouseId = s.FromWarehouseID
    ORDER BY ip.Id DESC
) lp

LEFT JOIN Suppliers sp   ON sp.Id     = lp.SupplierId
LEFT JOIN Warehouse whFrom ON whFrom.Id = s.FromWarehouseID
LEFT JOIN Warehouse whTo   ON whTo.Id   = s.ToWarehouseID
LEFT JOIN Bin bn           ON bn.Id     = s.BinId

LEFT JOIN ItemWarehouseStock iws
       ON iws.ItemId = s.ItemId
      AND iws.WarehouseId = s.FromWarehouseID
      AND (iws.BinId = s.BinId OR s.BinId IS NULL)

LEFT JOIN RS rs
       ON rs.ItemId      = s.ItemId
      AND rs.WarehouseId = s.FromWarehouseID

WHERE iws.IsTransfered =1
ORDER BY s.FromWarehouseID, lp.SupplierId;

";
            return await Connection.QueryAsync<StockTransferListViewInfo>(query);
        }




        public async Task<int> AdjustOnHandAsync(AdjustOnHandRequest request)
        {
            const string updateIwsSql = @"
UPDATE iws
SET 
    iws.OnHand = iws.OnHand - @FaultQty,   -- ✅ subtract fault qty
    iws.Available = CASE 
                        WHEN (iws.OnHand - @FaultQty) - ISNULL(iws.Reserved, 0) < 0 
                        THEN 0 
                        ELSE (iws.OnHand - @FaultQty) - ISNULL(iws.Reserved, 0) 
                    END,
    iws.StockIssueID = @StockIssueId,
    iws.ApprovedBy = @ApprovedBy
FROM [Finance].[dbo].[ItemWarehouseStock] iws
WHERE iws.ItemId = @ItemId
  AND iws.WarehouseId = @WarehouseId
  AND ((iws.BinId IS NULL AND @BinId IS NULL) OR iws.BinId = @BinId);
";

            const string updateItemPriceSql = @"
UPDATE ip
SET ip.Qty = @FinalOnHand     -- ✅ update full new on-hand
FROM [Finance].[dbo].[ItemPrice] ip
WHERE ip.ItemId = @ItemId
  AND ip.WarehouseId = @WarehouseId
  AND ip.SupplierId = @SupplierId;
";

            var conn = (SqlConnection)Connection;

            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            using var tx = conn.BeginTransaction();
            try
            {
                // 1️⃣ Update ItemWarehouseStock (subtract fault quantity)
                var rowsIws = await conn.ExecuteAsync(updateIwsSql, new
                {
                    request.ItemId,
                    request.WarehouseId,
                    request.BinId,
                    request.FaultQty,
                    request.StockIssueId,
                    request.ApprovedBy
                }, tx);

                // 2️⃣ Update ItemPrice (set full final on-hand)
                var rowsIp = await conn.ExecuteAsync(updateItemPriceSql, new
                {
                    request.ItemId,
                    request.WarehouseId,
                    request.SupplierId,
                    request.FinalOnHand
                }, tx);

                tx.Commit();
                return rowsIws + rowsIp;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
            finally
            {
                await conn.CloseAsync();
            }
        }





        public async Task<ApproveBulkResult> ApproveTransfersBulkAsync(IEnumerable<ApproveTransferRequest> requests)
        {
            var result = new ApproveBulkResult();
            var conn = (SqlConnection)Connection;

            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            using var tx = conn.BeginTransaction();
            try
            {
                int countIws = 0;
                int countStock = 0;
                int countItemPrice = 0;
                int countToWarehouse = 0;

                foreach (var req in requests)
                {
                    // 1) Get current Available & OnHand from FROM warehouse (bin-aware)
                    const string getAvailableSql = @"
SELECT Available, OnHand
FROM [dbo].[ItemWarehouseStock]
WHERE ItemId = @ItemId
  AND WarehouseId = @WarehouseId
  AND (BinId = @BinId OR (@BinId IS NULL AND BinId IS NULL));";

                    var current = await conn.QueryFirstOrDefaultAsync<(decimal Available, decimal OnHand)>(
                        getAvailableSql, req, tx);

                    if (current == default) continue;

                    // 2) Compute new FROM values
                    var newAvailable = current.Available - req.TransferQty;
                    var newOnHand = current.OnHand - req.TransferQty;

                    // 3) Update FROM ItemWarehouseStock
                    const string updateIwsSql = @"
UPDATE [dbo].[ItemWarehouseStock]
SET 
    IsApproved        = 1,
    Available         = @NewAvailable,
    OnHand            = @NewOnHand,
    IsFullTransfer    = @IsFullTransfer,
    IsPartialTransfer = @IsPartialTransfer,
    IsTransfered      = CASE 
                           WHEN @IsPartialTransfer = 1 THEN 0
                           ELSE IsTransfered
                        END
WHERE ItemId = @ItemId
  AND WarehouseId = @WarehouseId
  AND (BinId = @BinId OR (@BinId IS NULL AND BinId IS NULL));";

                    countIws += await conn.ExecuteAsync(updateIwsSql, new
                    {
                        req.ItemId,
                        req.WarehouseId,
                        req.BinId,
                        req.ToWarehouseId,
                        req.IsFullTransfer,
                        req.IsPartialTransfer,
                        NewAvailable = newAvailable,
                        NewOnHand = newOnHand
                    }, tx);

                    // 4) Update Stock
                    const string sqlStock = @"
UPDATE [dbo].[Stock]
SET 
    ToWarehouseID     = @ToWarehouseId,
    ToBinId           = @ToBinId,
    IsApproved        = 1,
    Remarks           = COALESCE(@Remarks, Remarks),
    Available         = Available - @TransferQty,
    OnHand            = OnHand - @TransferQty,
    TransferQty       = @TransferQty
WHERE Id = @StockId;";

                    countStock += await conn.ExecuteAsync(sqlStock, req, tx);

                    // 5) Update ItemPrice (FROM side)
                    const string sqlItemPrice = @"
UPDATE [dbo].[ItemPrice]
SET 
    Qty = Qty - @TransferQty,
    IsTransfered = CASE 
                      WHEN @IsPartialTransfer = 1 THEN 0 
                      ELSE IsTransfered
                   END
WHERE ItemId = @ItemId
  AND WarehouseId = @WarehouseId
  AND (
        (@SupplierId IS NULL AND SupplierId IS NULL)
        OR (SupplierId = @SupplierId)
      );";

                    countItemPrice += await conn.ExecuteAsync(sqlItemPrice, req, tx);

                    // ------------------- TO warehouse handling -------------------

                    // 6) Upsert ItemWarehouseStock at TO (bin-aware)
                    const string checkToWarehouseSql = @"
SELECT COUNT(*)
FROM .[dbo].[ItemWarehouseStock]
WHERE ItemId = @ItemId 
  AND WarehouseId = @ToWarehouseId
  AND (BinId = @ToBinId OR (@ToBinId IS NULL AND BinId IS NULL));";

                    var existsIwsTo = await conn.ExecuteScalarAsync<int>(checkToWarehouseSql, req, tx);

                    if (existsIwsTo > 0)
                    {
                        const string updateToWarehouseSql = @"
UPDATE [dbo].[ItemWarehouseStock]
SET 
    Available = Available + @TransferQty,
    OnHand    = OnHand + @TransferQty
WHERE ItemId = @ItemId 
  AND WarehouseId = @ToWarehouseId
  AND (BinId = @ToBinId OR (@ToBinId IS NULL AND BinId IS NULL));";

                        countToWarehouse += await conn.ExecuteAsync(updateToWarehouseSql, req, tx);
                    }
                    else
                    {
                        const string insertToWarehouseSql = @"
INSERT INTO [dbo].[ItemWarehouseStock]
(
    ItemId,
    WarehouseId,
    BinId,
    StrategyId,
    OnHand,
    Reserved,
    MinQty,
    MaxQty,
    ReorderQty,
    LeadTimeDays,
    BatchFlag,
    SerialFlag,
    Available,
    IsApproved,
    IsTransfered,
    IsFullTransfer,
    IsPartialTransfer,
    StockIssueID
)
VALUES
(
    @ItemId,
    @ToWarehouseId,
    @ToBinId,
    NULL,
    @TransferQty,
    0,
    0,
    0,
    0,
    0,
    0,
    0,
    @TransferQty,
    0,
    0,
    0,
    0,
    0
);";

                        countToWarehouse += await conn.ExecuteAsync(insertToWarehouseSql, req, tx);
                    }

                    // 7) ItemPrice at TO warehouse
                    //    a) Check existence (supplier-aware)
                    const string checkToItemPriceSql = @"
SELECT COUNT(*) 
FROM [dbo].[ItemPrice]
WHERE ItemId = @ItemId 
  AND WarehouseId = @ToWarehouseId
  AND (
       (@SupplierId IS NULL AND SupplierId IS NULL)
       OR (SupplierId = @SupplierId)
      );";

                    var existsItemPrice = await conn.ExecuteScalarAsync<int>(checkToItemPriceSql, req, tx);

                    if (existsItemPrice > 0)
                    {
                        // b) Update Qty only
                        const string updateToItemPriceSql = @"
UPDATE [dbo].[ItemPrice]
SET 
    Qty = Qty + @TransferQty,
    UpdatedDate = GETDATE(),
    IsTransfered = 0
WHERE ItemId = @ItemId 
  AND WarehouseId = @ToWarehouseId
  AND (
       (@SupplierId IS NULL AND SupplierId IS NULL)
       OR (SupplierId = @SupplierId)
      );";

                        countItemPrice += await conn.ExecuteAsync(updateToItemPriceSql, req, tx);
                    }
                    else
                    {
                        // c) INSERT with PRICE taken from FROM warehouse's latest ItemPrice
                        const string getSrcPriceSql = @"
SELECT TOP (1) Price, Barcode
FROM [dbo].[ItemPrice]
WHERE ItemId = @ItemId 
  AND WarehouseId = @WarehouseId
  AND (
       (@SupplierId IS NULL AND SupplierId IS NULL)
       OR (SupplierId = @SupplierId)
      )
ORDER BY 
    ISNULL(UpdatedDate, CreatedDate) DESC, Id DESC;";

                        var src = await conn.QueryFirstOrDefaultAsync<(decimal? Price, string Barcode)>(getSrcPriceSql, req, tx);
                        var price = src.Price ?? 0m;
                        var barcode = (object?)src.Barcode ?? DBNull.Value;

                        const string insertToItemPriceSql = @"
INSERT INTO [dbo].[ItemPrice]
(
    ItemId,
    SupplierId,
    Price,
    Barcode,
    Qty,
    CreatedBy,
    CreatedDate,
    UpdatedBy,
    UpdatedDate,
    WarehouseId,
    IsTransfered
)
VALUES
(
    @ItemId,
    @SupplierId,
    @Price,          -- <-- carry price
    @Barcode,        -- <-- carry barcode (if any)
    @TransferQty,
    'System',
    GETDATE(),
    NULL,
    NULL,
    @ToWarehouseId,
    0                -- mark as transferred/created by transfer
);";

                        countItemPrice += await conn.ExecuteAsync(insertToItemPriceSql, new
                        {
                            req.ItemId,
                            req.SupplierId,
                            Price = price,
                            Barcode = barcode,
                            req.TransferQty,
                            req.ToWarehouseId
                        }, tx);
                    }
                    // ------------------- /TO warehouse handling -------------------
                }

                tx.Commit();

                result.UpdatedItemWarehouseStock = countIws;
                result.UpdatedStock = countStock;
                result.UpdatedItemPrice = countItemPrice;
                result.UpdatedToWarehouse = countToWarehouse;

                return result;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
            finally
            {
                await conn.CloseAsync();
            }
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
    s.TransferQty,
    s.Available
FROM Stock AS s
INNER JOIN ItemMaster AS im 
    ON im.Id = s.ItemId
INNER JOIN ItemWarehouseStock AS iws 
    ON iws.ItemId = s.ItemId 
    AND iws.WarehouseId = s.FromWarehouseID   -- ✅ restrict to one warehouse
LEFT JOIN Warehouse AS whFrom 
    ON whFrom.Id = s.FromWarehouseID
LEFT JOIN Warehouse AS whTo 
    ON whTo.Id = s.ToWarehouseID
WHERE   s.Id = @Id ;
";
            return await Connection.QuerySingleAsync<StockHistoryViewInfo>(query, new { Id = id });
        }



    }
}
