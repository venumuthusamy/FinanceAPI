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

        public async Task<int> CreateAsync(PurchaseGoodReceiptItemsDTO goodReceiptItemsDTO)
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
            (POID, ReceptionDate, OverReceiptTolerance, GRNJson, FlagIssuesID, GrnNo) 
        OUTPUT INSERTED.Id 
        VALUES (@POID, @ReceptionDate, @OverReceiptTolerance, @GRNJson, @FlagIssuesID, @GrnNo)";

            return await Connection.QueryFirstAsync<int>(insertQuery, goodReceiptItemsDTO);
        }


    }
}
