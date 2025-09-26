﻿using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace FinanceApi.Repositories
{
    public class CurrencyRepository : DynamicRepository,ICurrencyRepository
    {
        private readonly ApplicationDbContext _context;

        public CurrencyRepository(IDbConnectionFactory connectionFactory)
      : base(connectionFactory)
        {
        }
        public async Task<IEnumerable<CurrencyDTO>> GetAllAsync()
        {
            const string query = @"
                SELECT * from Currency";

            return await Connection.QueryAsync<CurrencyDTO>(query);
        }


        public async Task<CurrencyDTO> GetByIdAsync(int id)
        {

            const string query = "SELECT * FROM Currency WHERE Id = @Id";

            return await Connection.QuerySingleAsync<CurrencyDTO>(query, new { Id = id });
        }

        public async Task<int> CreateAsync(Currency currencyDTO)
        {
            const string query = @"INSERT INTO Currency (CurrencyName,Description,CreatedBy, CreatedDate, UpdatedBy, UpdatedDate,IsActive) 
                               OUTPUT INSERTED.Id 
                               VALUES (@CurrencyName,@Description,@CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate,@IsActive)";
            return await Connection.QueryFirstAsync<int>(query, currencyDTO);
        }


        public async Task UpdateAsync(Currency currencyDTO)
        {
            const string query = "UPDATE Currency SET CurrencyName = @CurrencyName, Description = @Description WHERE Id = @Id";
            await Connection.ExecuteAsync(query, currencyDTO);
        }

        public async Task DeactivateAsync(int id)
        {
            const string query = "UPDATE Currency SET IsActive = 0 WHERE ID = @id";
            await Connection.ExecuteAsync(query, new { ID = id });
        }
    }
}
