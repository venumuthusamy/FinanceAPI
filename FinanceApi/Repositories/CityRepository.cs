using FinanceApi.Data;
using FinanceApi.Interfaces;
using Dapper;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace FinanceApi.Repositories
{
    public class CityRepository : DynamicRepository,ICityRepository
    {
        private readonly ApplicationDbContext _context;

        public CityRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
        {
        }

        public async Task<IEnumerable<CityDto>> GetAllAsync()
        {
            const string query = @" SELECT * from City Where isActive = 1";

            return await Connection.QueryAsync<CityDto>(query);
        }


        public async Task<CityDto> GetByIdAsync(long id)
        {

            const string query = "SELECT * FROM City WHERE Id = @Id";

            return await Connection.QuerySingleAsync<CityDto>(query, new { Id = id });
        }

        public async Task<int> CreateAsync(City city)
        {
            const string query = @"INSERT INTO City (CityName,StateId,CountryId,CreatedBy, CreatedDate, UpdatedBy, UpdatedDate,IsActive) 
                               OUTPUT INSERTED.Id 
                               VALUES (@CityName,@StateId,@CountryId,@CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate,@IsActive)";
            return await Connection.QueryFirstAsync<int>(query, city);
        }


        public async Task UpdateAsync(City city)
        {
            const string query = @"UPDATE City 
                       SET CountryId = @CountryId, StateId = @StateId, CityName = @CityName
                       WHERE Id = @Id";
            await Connection.ExecuteAsync(query, city);
        }

        public async Task DeactivateAsync(int id)
        {
            const string query = "UPDATE City SET IsActive = 0 WHERE ID = @id";
            await Connection.ExecuteAsync(query, new { ID = id });
        }


        public async Task<CityDto> GetStateWithCountryId(long id)
        {

            const string query = "SELECT s.Id, s.StateName FROM State AS s INNER JOIN Country AS c ON c.Id = s.CountryId WHERE c.Id = @id and s.isActive = 1;";

            return await Connection.QuerySingleAsync<CityDto>(query, new { Id = id });
        }
    }
}
