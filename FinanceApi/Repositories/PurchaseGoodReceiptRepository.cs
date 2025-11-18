using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.AspNetCore.Connections;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace FinanceApi.Repositories
{
    public class PurchaseGoodReceiptRepository : DynamicRepository, IPurchaseGoodReceiptRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;
        public PurchaseGoodReceiptRepository(IDbConnectionFactory connectionFactory)
         : base(connectionFactory)
        {
            _connectionFactory = connectionFactory; // store it for later use
        }

        public async Task<IEnumerable<PurchaseGoodReceiptItemsDTO>> GetAllAsync()
        {
            const string query = @"
SELECT
    pg.*,
    po.PoLines,
    po.CurrencyId,
    po.Tax,
    s.Id   AS SupplierId,
    s.Name AS SupplierName
FROM PurchaseGoodReceipt AS pg
JOIN PurchaseOrder AS po ON po.Id = pg.POID
LEFT JOIN Suppliers AS s   ON s.Id = po.SupplierId
WHERE pg.Id NOT IN (
    SELECT GrnId
    FROM SupplierInvoicePin
    WHERE IsActive = 0
      AND GrnId IS NOT NULL
);
";

            return await Connection.QueryAsync<PurchaseGoodReceiptItemsDTO>(query);
        }

        public async Task<PurchaseGoodReceiptItemsDTO> GetByIdAsync(long id)
        {
            const string query = "SELECT * FROM PurchaseGoodReceipt WHERE Id = @Id";
            return await Connection.QuerySingleAsync<PurchaseGoodReceiptItemsDTO>(query, new { Id = id });
        }

        public async Task<int> CreateAsync(PurchaseGoodReceiptItems goodReceiptItemsDTO)
        {
            // 1. Get last GRN
            const string getLastGRNQuery = @"SELECT TOP 1 GrnNo FROM PurchaseGoodReceipt ORDER BY Id DESC";
            var lastGRN = await Connection.QueryFirstOrDefaultAsync<string>(getLastGRNQuery);
            int nextNumber = 1;

            // 2. Increment number
            if (!string.IsNullOrWhiteSpace(lastGRN) && lastGRN.StartsWith("GRN-"))
            {
                var numericPart = lastGRN.Substring(4);
                if (int.TryParse(numericPart, out int lastNumber))
                    nextNumber = lastNumber + 1;
            }

            // 3. Format GRN No
            var newGRN = $"GRN-{nextNumber:D4}";
            goodReceiptItemsDTO.GrnNo = newGRN;

            // 4. Insert
            const string insertQuery = @"
INSERT INTO PurchaseGoodReceipt 
    (POID, ReceptionDate, OverReceiptTolerance, GRNJson, GrnNo, isActive) 
OUTPUT INSERTED.Id 
VALUES (@POID, @ReceptionDate, @OverReceiptTolerance, @GRNJson, @GrnNo, @isActive)";

            return await Connection.QueryFirstAsync<int>(insertQuery, goodReceiptItemsDTO);
        }

        public async Task<IEnumerable<PurchaseGoodReceiptItemsViewInfo>> GetAllDetailsAsync()
        {
            const string query = @"
SELECT 
  pg.Id AS ID,
  pg.ReceptionDate,
  po.PurchaseOrderNo AS PONO,
  pg.GrnNo,
  gd.itemCode,
  i.itemName AS ItemName,
  gd.supplierId,
  s.Name AS Name,
  gd.storageType,
  gd.surfaceTemp,
  gd.expiry,
  gd.pestSign,
  gd.drySpillage,
  gd.odor,
  gd.plateNumber,
  gd.defectLabels,
  gd.damagedPackage,
  gd.[time],
  gd.initial,
  gd.isFlagIssue,
  gd.isPostInventory,
  gd.qtyReceived,
  gd.qualityCheck,
  gd.batchSerial,
  gd.warehouseId,
  gd.binId,
  gd.strategyId,
  gd.warehouseName,
  gd.binName,
  gd.strategyName
FROM PurchaseGoodReceipt pg
OUTER APPLY OPENJSON(pg.GRNJson)
WITH (
    itemCode        NVARCHAR(50)  '$.itemCode',
    supplierId      INT           '$.supplierId',
    storageType     NVARCHAR(50)  '$.storageType',
    surfaceTemp     NVARCHAR(50)  '$.surfaceTemp',
    expiry          DATE          '$.expiry',
    pestSign        NVARCHAR(50)  '$.pestSign',
    drySpillage     NVARCHAR(50)  '$.drySpillage',
    odor            NVARCHAR(50)  '$.odor',
    plateNumber     NVARCHAR(50)  '$.plateNumber',
    defectLabels    NVARCHAR(100) '$.defectLabels',
    damagedPackage  NVARCHAR(50)  '$.damagedPackage',
    [time]          DATETIME2     '$.time',
    initial         NVARCHAR(MAX) '$.initial',
    isFlagIssue     BIT           '$.isFlagIssue',
    isPostInventory BIT           '$.isPostInventory',
    qtyReceived     int '$.qtyReceived',
    qualityCheck    NVARCHAR(MAX) '$.qualityCheck',
    batchSerial     NVARCHAR(MAX) '$.batchSerial',
	warehouseId     INT '$.warehouseId',
    binId           INT '$.binId',
	strategyId      INT '$.strategyId',
    warehouseName   NVARCHAR(MAX) '$.warehouseName',
	binName         NVARCHAR(MAX) '$.binName',
    strategyName    NVARCHAR(MAX) '$.strategyName'
) AS gd
LEFT JOIN item i ON gd.itemCode = i.itemCode
LEFT JOIN Suppliers s ON gd.supplierId = s.Id
LEFT JOIN PurchaseOrder PO ON pg.POID = PO.Id
ORDER BY pg.Id DESC;";

            return await Connection.QueryAsync<PurchaseGoodReceiptItemsViewInfo>(query);
        }

        public async Task UpdateAsync(PurchaseGoodReceiptItems purchaseGoodReceipt)
        {
            const string query = "UPDATE PurchaseGoodReceipt SET GRNJSON = @GRNJSON WHERE Id = @ID";
            await Connection.ExecuteAsync(query, purchaseGoodReceipt);
        }

        public async Task UpdateGRN(PurchaseGoodReceiptItemsDTO purchaseGoodReceipt)
        {
            const string query = "UPDATE PurchaseGoodReceipt SET GRNJSON = @GRNJSON WHERE Id = @ID";
            await Connection.ExecuteAsync(query, purchaseGoodReceipt);
        }

        public async Task DeactivateAsync(int id)
        {
            const string query = "UPDATE PurchaseGoodReceipt SET IsActive = 0 WHERE ID = @id";
            await Connection.ExecuteAsync(query, new { ID = id });
        }

        public async Task<IEnumerable<PurchaseGoodReceiptItemsDTO>> GetAllGRN()
        {
            const string query = @"
SELECT ID, POID, ReceptionDate, OverReceiptTolerance, GRNJson, FlagIssuesID, GrnNo
FROM PurchaseGoodReceipt
ORDER BY ID";
            return await Connection.QueryAsync<PurchaseGoodReceiptItemsDTO>(query);
        }

        public async Task<IEnumerable<PurchaseGoodReceiptItemsDTO>> GetAllGRNByPoId()
        {
            const string query = @"
SELECT 
     pgr.ID,
     pgr.POID,
     pgr.ReceptionDate,
     pgr.OverReceiptTolerance,
     pgr.GRNJson,
     pgr.GrnNo,
     po.Tax
FROM PurchaseGoodReceipt as pgr 
INNER JOIN PurchaseOrder as po ON po.id= pgr.POID
ORDER BY pgr.ID";
            return await Connection.QueryAsync<PurchaseGoodReceiptItemsDTO>(query);
        }

        // 🔹 Public method called from Service
        public async Task ApplyGrnAndUpdateSalesOrderAsync(string itemCode, int? warehouseId, int? supplierId, int? binId, decimal receivedQty)
        {
            var itemId = await GetItemIdByCodeAsync(itemCode);
            if (itemId == null)
                throw new ArgumentException("Invalid ItemCode.");

            using var conn = _connectionFactory.CreateConnection();
            conn.Open(); // ✅ sync open

            using var tx = conn.BeginTransaction();
            try
            {
                await UpdateSalesOrderLinesAndAlertAsync(conn, tx, itemId.Value, warehouseId, supplierId, binId, receivedQty);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }


        // 🔹 Get ItemId from ItemCode
        private async Task<int?> GetItemIdByCodeAsync(string itemCode)
        {
            using var conn = _connectionFactory.CreateConnection();
            conn.Open(); // ✅ sync open

            const string sql = @"
                SELECT TOP (1) Id 
                FROM dbo.Item 
                WHERE ItemCode = @ItemCode AND IsActive = 1;";

            return await conn.ExecuteScalarAsync<int?>(sql, new { ItemCode = itemCode });
        }


        // 🔹 Update both SalesOrderLines + PurchaseAlert
        private async Task UpdateSalesOrderLinesAndAlertAsync(
             IDbConnection conn,
             IDbTransaction tx,
             int itemId,
             int? warehouseId,
             int? supplierId,
             int? binId,
             decimal receivedQty)
        {
            const string sql = @"
----------------------------------------------------------
-- 1️⃣ Update SalesOrderLines when certain conditions match
----------------------------------------------------------
UPDATE sol
SET
    sol.WarehouseId = @WarehouseId,
    sol.SupplierId  = @SupplierId,
    sol.BinId       = @BinId,

    sol.Available = CASE 
                 WHEN @ReceivedQty > sol.LockedQty THEN sol.LockedQty
                 ELSE @ReceivedQty 
              END,
    sol.Quantity = CASE 
                 WHEN @ReceivedQty > sol.LockedQty THEN sol.LockedQty
                 ELSE @ReceivedQty 
              END,
    sol.UpdatedDate = GETDATE()
FROM dbo.SalesOrderLines sol
WHERE 
    sol.ItemId = @ItemId
    AND sol.Quantity = 0
    AND sol.LockedQty > 0
    AND sol.WarehouseId IS NULL
    AND sol.SupplierId IS NULL
    AND sol.BinId IS NULL;

----------------------------------------------------------
-- 2️⃣ Update PurchaseAlert with GRN logic
----------------------------------------------------------
UPDATE pa
SET 
    pa.WarehouseId = @WarehouseId,
    pa.SupplierId = @SupplierId,
    pa.RequiredQty = CASE 
                        WHEN @ReceivedQty < pa.RequiredQty 
                            THEN pa.RequiredQty - @ReceivedQty
                        ELSE pa.RequiredQty
                     END,
    pa.IsRead = CASE 
                    WHEN @ReceivedQty >= pa.RequiredQty THEN 1 
                    ELSE 0 
                END
FROM dbo.PurchaseAlert pa
WHERE 
    pa.ItemId = @ItemId
    AND pa.IsRead = 0;
";

            await conn.ExecuteAsync(sql, new
            {
                ItemId = itemId,
                WarehouseId = warehouseId,
                SupplierId = supplierId,
                BinId = binId,
                ReceivedQty = receivedQty
            }, tx);
        }
    }
}
