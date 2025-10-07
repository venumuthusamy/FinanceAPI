﻿using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.Models;

namespace FinanceApi.Repositories
{
    public class TaxCodeRepository : DynamicRepository, ITaxCodeRepository
    {
        private readonly ApplicationDbContext _context;

        public TaxCodeRepository(IDbConnectionFactory connectionFactory)
       : base(connectionFactory)
        {
        }

        public async Task<IEnumerable<TaxCode>> GetAllAsync()
        {
            const string query = @"
                SELECT * from TaxCode Where isActive = 'true' ";

            return await Connection.QueryAsync<TaxCode>(query);
        }


        public async Task<TaxCode> GetByIdAsync(int id)
        {

            const string query = "SELECT * FROM TaxCode WHERE Id = @Id";

            return await Connection.QuerySingleAsync<TaxCode>(query, new { Id = id });
        }
        public async Task<int> CreateAsync(TaxCode taxCode)
        {
            const string query = @"
        INSERT INTO TaxCode 
            (Name, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive) 
        OUTPUT INSERTED.Id 
        VALUES 
            (@Name, @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, @IsActive)
    ";

            var parameters = new DynamicParameters();
            parameters.Add("@Name", taxCode.Name);
            parameters.Add("@CreatedBy", taxCode.CreatedBy);
            parameters.Add("@CreatedDate", taxCode.CreatedDate);
            parameters.Add("@UpdatedBy", taxCode.UpdatedBy);
            parameters.Add("@UpdatedDate", taxCode.UpdatedDate);
            parameters.Add("@IsActive", taxCode.IsActive);

            return await Connection.QueryFirstAsync<int>(query, parameters);
        }




        public async Task UpdateAsync(TaxCode taxCode)
        {
            const string query = "UPDATE TaxCode SET Name = @Name WHERE Id = @Id";
            await Connection.ExecuteAsync(query, taxCode);
        }

        public async Task DeactivateAsync(int id)
        {
            const string query = "UPDATE TaxCode SET IsActive = 0 WHERE ID = @id";
            await Connection.ExecuteAsync(query, new { ID = id });
        }
    }
}
