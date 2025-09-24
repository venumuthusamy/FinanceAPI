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
                    FlagIssuesID
                FROM PurchaseGoodReceipt
                ORDER BY ID";

            return await Connection.QueryAsync<PurchaseGoodReceiptItemsDTO>(query); 
        }


        public async Task<PurchaseGoodReceiptItemsDTO> GetByIdAsync(long id)
        {

            const string query = "SELECT * FROM PurchaseGoodReceipt WHERE Id = @Id";

            return await Connection.QuerySingleAsync<PurchaseGoodReceiptItemsDTO>(query, new { Id = id });
        }

        public async Task<int> CreateAsync(PurchaseGoodReceiptItemsDTO goodReceiptItemsDTO)
        {
            const string query = @"INSERT INTO PurchaseGoodReceipt (POID, ReceptionDate, OverReceiptTolerance, GRNJson,FlagIssuesID) 
                               OUTPUT INSERTED.Id 
                               VALUES (@POID, @ReceptionDate, @OverReceiptTolerance, @GRNJson,@FlagIssuesID)";
            return await Connection.QueryFirstAsync<int>(query, goodReceiptItemsDTO);
        }


    }
}
