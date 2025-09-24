using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;

namespace FinanceApi.Repositories
{
    public class FlagIssuesRepository : DynamicRepository, IFlagIssuesRepository
    {
        public FlagIssuesRepository(IDbConnectionFactory connectionFactory)
     : base(connectionFactory)
        {
        }


        public async Task<IEnumerable<FlagIssuesDTO>> GetAllAsync()
        {
            const string query = @"
                SELECT 
                    ID,
                    CreatedBy,
                    CreatedDate,
                    UpdatedBy,
                    UpdatedDate,
                    IsActive,
                    FlagIssuesNames
                FROM FlagIssues
                ORDER BY ID";

            return await Connection.QueryAsync<FlagIssuesDTO>(query);
        }


        public async Task<FlagIssuesDTO> GetByIdAsync(long id)
        {

            const string query = "SELECT * FROM FlagIssues WHERE Id = @Id";

            return await Connection.QuerySingleAsync<FlagIssuesDTO>(query, new { Id = id });
        }

        public async Task<int> CreateAsync(FlagIssuesDTO flagIssuesDTO)
        {
            const string query = @"INSERT INTO FlagIssues (FlagIssuesNames,CreatedBy, CreatedDate, UpdatedBy, UpdatedDate,IsActive) 
                               OUTPUT INSERTED.Id 
                               VALUES (@FlagIssuesNames,@CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate,@IsActive)";
            return await Connection.QueryFirstAsync<int>(query, flagIssuesDTO);
        }


        public async Task UpdateAsync(FlagIssuesDTO flagIssuesDTO)
        {
            const string query = "UPDATE FlagIssues SET FlagIssuesNames = @FlagIssuesNames WHERE Id = @Id";
            await Connection.ExecuteAsync(query, flagIssuesDTO);
        }

        public async Task DeactivateAsync(int id)
        {
            const string query = "UPDATE FlagIssues SET IsActive = 0 WHERE ID = @id";
            await Connection.ExecuteAsync(query, new { ID = id });
        }
    }
}
