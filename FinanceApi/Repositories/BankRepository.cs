using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Repositories
{
    public class BankRepository : DynamicRepository,IBankRepository
    {
        public BankRepository(IDbConnectionFactory connectionFactory)
: base(connectionFactory)
        {
        }


        public async Task<IEnumerable<BankDto>> GetAllAsync()
        {
            const string query = @"
                SELECT * from Bank
where isActive = 1
                ORDER BY ID
";

            return await Connection.QueryAsync<BankDto>(query);
        }


        public async Task<BankDto> GetByIdAsync(long id)
        {

            const string query = "SELECT * FROM Bank WHERE Id = @Id";

            return await Connection.QuerySingleAsync<BankDto>(query, new { Id = id });
        }

        public async Task<int> CreateAsync(Bank bankDto)
        {
            const string query = @"
        INSERT INTO Bank 
        (
            BankName,
            AccountHolderName,
            AccountNo,
            AccountType,
            Branch,
            IFSC,
            Routing,
            CurrencyId,
            CountryId,
            PrimaryContact,
            Email,
            ContactNo,
            Address,
            IsActive,
            BudgetLineId
        )
        OUTPUT INSERTED.Id
        VALUES
        (
            @BankName,
            @AccountHolderName,
            @AccountNo,
            @AccountType,
            @Branch,
            @IFSC,
            @Routing,
            @CurrencyId,
            @CountryId,
            @PrimaryContact,
            @Email,
            @ContactNo,
            @Address,
            @IsActive,
            @BudgetLineId
        )";

            return await Connection.QueryFirstAsync<int>(query, bankDto);
        }



        public async Task UpdateAsync(Bank bankDto)
        {
            const string query = @"
        UPDATE Bank SET 
            BankName = @BankName,
            AccountHolderName = @AccountHolderName,
            AccountNo = @AccountNo,
            AccountType = @AccountType,
            Branch = @Branch,
            IFSC = @IFSC,
            Routing = @Routing,
            CurrencyId = @CurrencyId,
            CountryId = @CountryId,
            PrimaryContact = @PrimaryContact,
            Email = @Email,
            ContactNo = @ContactNo,
            Address = @Address,
            BudgetLineId = @BudgetLineId
        WHERE Id = @Id";

            await Connection.ExecuteAsync(query, bankDto);
        }


        public async Task DeactivateAsync(int id)
        {
            const string query = "UPDATE Bank SET IsActive = 0 WHERE ID = @id";
            await Connection.ExecuteAsync(query, new { ID = id });
        }
    }
}
