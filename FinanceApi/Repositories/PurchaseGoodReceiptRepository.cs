using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

namespace FinanceApi.Repositories
{
    public class PurchaseGoodReceiptRepository : DynamicRepository, IPurchaseGoodReceiptRepository
    {
        public PurchaseGoodReceiptRepository(IDbConnectionFactory connectionFactory)
         : base(connectionFactory)
        {
        }




        public async Task<IEnumerable<PurchaseGoodReceiptItemsDTO>> GetAllAsync()
        {
            //const string query = @"
            //    SELECT 
            //        ID,
            //        POID,
            //        ReceptionDate,
            //        OverReceiptTolerance,
            //        GRNJson,
            //        FlagIssuesID,
            //        GrnNo
            //    FROM PurchaseGoodReceipt
            //    ORDER BY ID";

            const string query = @"SELECT pg.*,po.PoLines,po.CurrencyId FROM PurchaseGoodReceipt as pg 
                                   inner join PurchaseOrder as po on pg.POID=po.Id";

            return await Connection.QueryAsync<PurchaseGoodReceiptItemsDTO>(query); 
        }


        public async Task<PurchaseGoodReceiptItemsDTO> GetByIdAsync(long id)
        {

            const string query = "SELECT * FROM PurchaseGoodReceipt WHERE Id = @Id";

            return await Connection.QuerySingleAsync<PurchaseGoodReceiptItemsDTO>(query, new { Id = id });
        }

        //public async Task<int> CreateAsync(PurchaseGoodReceiptItemsDTO goodReceiptItemsDTO)
        //{
        //    const string query = @"INSERT INTO PurchaseGoodReceipt (POID, ReceptionDate, OverReceiptTolerance, GRNJson,FlagIssuesID) 
        //                       OUTPUT INSERTED.Id 
        //                       VALUES (@POID, @ReceptionDate, @OverReceiptTolerance, @GRNJson,@FlagIssuesID)";
        //    return await Connection.QueryFirstAsync<int>(query, goodReceiptItemsDTO);
        //}

        public async Task<int> CreateAsync(PurchaseGoodReceiptItems goodReceiptItemsDTO)
        {
            // Step 1: Get the last GrnNo from the database
            const string getLastGRNQuery = @"SELECT TOP 1 GrnNo FROM PurchaseGoodReceipt ORDER BY Id DESC";
            var lastGRN = await Connection.QueryFirstOrDefaultAsync<string>(getLastGRNQuery);

            int nextNumber = 1;

            // Step 2: Parse the last GRN number and increment it
            if (!string.IsNullOrWhiteSpace(lastGRN) && lastGRN.StartsWith("GRN-"))
            {
                var numericPart = lastGRN.Substring(4);
                if (int.TryParse(numericPart, out int lastNumber))
                {
                    nextNumber = lastNumber + 1;
                }
            }

            // Step 3: Format the new GRN number
            var newGRN = $"GRN-{nextNumber.ToString("D4")}";

            // Step 4: Assign to DTO
            goodReceiptItemsDTO.GrnNo = newGRN;

            // Step 5: Insert new record with generated GRN number
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
  gd.isFlagIssue
FROM PurchaseGoodReceipt pg
OUTER APPLY OPENJSON(pg.GRNJson)
WITH (
    itemCode       NVARCHAR(50)  '$.itemCode',
    supplierId     INT           '$.supplierId',
    storageType    NVARCHAR(50)  '$.storageType',
    surfaceTemp    NVARCHAR(50)  '$.surfaceTemp',
    expiry         DATE          '$.expiry',
    pestSign       NVARCHAR(50)  '$.pestSign',
    drySpillage    NVARCHAR(50)  '$.drySpillage',
    odor           NVARCHAR(50)  '$.odor',
    plateNumber    NVARCHAR(50)  '$.plateNumber',
    defectLabels   NVARCHAR(100) '$.defectLabels',
    damagedPackage NVARCHAR(50)  '$.damagedPackage',
    [time]         DATETIME2     '$.time',
    initial        NVARCHAR(Max) '$.initial',
    isFlagIssue    BIT           '$.isFlagIssue'
) AS gd
LEFT JOIN item i ON gd.itemCode = i.itemCode
LEFT JOIN Suppliers s ON gd.supplierId = s.Id
LEFT JOIN PurchaseOrder PO ON pg.POID = PO.Id

-- Only show active records
WHERE pg.isActive = 1

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
                SELECT 
                    ID,
                    POID,
                    ReceptionDate,
                    OverReceiptTolerance,
                    GRNJson,
                    FlagIssuesID,
                    GrnNo
                FROM PurchaseGoodReceipt
                ORDER BY ID";

            return await Connection.QueryAsync<PurchaseGoodReceiptItemsDTO>(query);
        }
    }
}
