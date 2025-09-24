using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;

namespace FinanceApi.Repositories
{
    public class IncotermsRepository : DynamicRepository,IIncotermsRepository
    {
        public IncotermsRepository(IDbConnectionFactory connectionFactory)
       : base(connectionFactory)
        {
        }

        public async Task<IEnumerable<IncotermsDTO>> GetAllAsync()
        {
            const string query = @"
                SELECT 
                    ID,
                    CreatedBy,
                    CreatedDate,
                    UpdatedBy,
                    UpdatedDate,
                    IsActive,
                    IncotermsName
                FROM Incoterms
                ORDER BY ID";

            return await Connection.QueryAsync<IncotermsDTO>(query);
        }


        public async Task<IncotermsDTO> GetByIdAsync(long id)
        {

            const string query = "SELECT * FROM Incoterms WHERE Id = @Id";

            return await Connection.QuerySingleAsync<IncotermsDTO>(query, new { Id = id });
        }

        public async Task<int> CreateAsync(IncotermsDTO incotermsDTO)
        {
            const string query = @"INSERT INTO Incoterms (IncotermsName,CreatedBy, CreatedDate, UpdatedBy, UpdatedDate,IsActive) 
                               OUTPUT INSERTED.Id 
                               VALUES (@IncotermsName,@CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate,@IsActive)";
            return await Connection.QueryFirstAsync<int>(query, incotermsDTO);
        }


        public async Task UpdateAsync(IncotermsDTO incotermsDTO)
        {
            const string query = "UPDATE Incoterms SET IncotermsName = @IncotermsName WHERE Id = @Id";
            await Connection.ExecuteAsync(query, incotermsDTO);
        }

        public async Task DeactivateAsync(int id)
        {
            const string query = "UPDATE Incoterms SET IsActive = 0 WHERE ID = @id";
            await Connection.ExecuteAsync(query, new { ID = id });
        }

    }
}
