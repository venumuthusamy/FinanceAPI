using FinanceApi.Data;
using Dapper;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace FinanceApi.Repositories
{
    public class StateRepository : DynamicRepository ,IStateRepository
    {
        private readonly ApplicationDbContext _context;

        public StateRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
        {
        }
        public async Task<IEnumerable<StateDto>> GetAllAsync()
        {
            const string query = @" SELECT * from State Where isActive = 1";

            return await Connection.QueryAsync<StateDto>(query);
        }


        public async Task<StateDto> GetByIdAsync(long id)
        {

            const string query = "SELECT * FROM State WHERE Id = @Id";

            return await Connection.QuerySingleAsync<StateDto>(query, new { Id = id });
        }

        public async Task<int> CreateAsync(State state)
        {
            const string query = @"INSERT INTO State (StateName,CountryId,CreatedBy, CreatedDate, UpdatedBy, UpdatedDate,IsActive) 
                               OUTPUT INSERTED.Id 
                               VALUES (@StateName,@CountryId,@CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate,@IsActive)";
            return await Connection.QueryFirstAsync<int>(query, state);
        }


        public async Task UpdateAsync(State state)
        {
            const string query = "UPDATE State SET StateName = @StateName,CountryId = @CountryId WHERE Id = @Id";
            await Connection.ExecuteAsync(query, state);
        }

        public async Task DeactivateAsync(int id)
        {
            const string query = "UPDATE State SET IsActive = 0 WHERE ID = @id";
            await Connection.ExecuteAsync(query, new { ID = id });
        }
    }
}
