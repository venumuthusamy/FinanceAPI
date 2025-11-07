using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Repositories
{
    public class CustomerMasterRepository : DynamicRepository, ICustomerMasterRepository
    {
        public CustomerMasterRepository(IDbConnectionFactory connectionFactory)
       : base(connectionFactory)
        {
        }

        public async Task<IEnumerable<CustomerMasterDTO>> GetAllAsync()
        {
            const string query = @"
                SELECT * from Customer";

            return await Connection.QueryAsync<CustomerMasterDTO>(query);
        }


        public async Task<CustomerMasterDTO> GetByIdAsync(int id)
        {

            const string query = "SELECT * FROM Customer WHERE Id = @Id";

            return await Connection.QuerySingleAsync<CustomerMasterDTO>(query, new { Id = id });
        }

        public async Task<int> CreateAsync(CustomerMaster customerMaster)
        {
            const string query = @"
        INSERT INTO Customer (
            CustomerName,
            CountryId,
            LocationId,
            ContactNumber,
            PointOfContactPerson,
            Email,
            CustomerGroupId,
            PaymentTermId,
            CreditAmount,
            KycId,
            CreatedDate,
            CreatedBy,
            UpdatedDate,
            UpdatedBy,
            IsActive
        )
        OUTPUT INSERTED.Id
        VALUES (
            @CustomerName,
            @CountryId,
            @LocationId
            @ContactNumber,
            @PointOfContactPerson,
            @Email,
            @CustomerGroupId,
            @PaymentTermId,
            @CreditAmount,
            @KycId,
            @CreatedDate,
            @CreatedBy,
            @UpdatedDate,
            @UpdatedBy,
            @IsActive
        )";

            return await Connection.QueryFirstAsync<int>(query, customerMaster);
        }



        public async Task UpdateAsync(CustomerMaster customerMaster)
        {
            const string query = "UPDATE Customer SET CustomerName = @CustomerName, LocationName =@LocationName WHERE Id = @Id";
            await Connection.ExecuteAsync(query, customerMaster);
        }

        public async Task DeactivateAsync(int id)
        {
            const string query = "UPDATE Customer SET IsActive = 0 WHERE ID = @id";
            await Connection.ExecuteAsync(query, new { ID = id });
        }
    }
}
