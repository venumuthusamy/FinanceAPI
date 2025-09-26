using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace FinanceApi.Repositories
{
    public class SuppliersRepository : DynamicRepository,ISuppliersRepository
    {
        private readonly ApplicationDbContext _context;

        // Add new supplier
        public SuppliersRepository(IDbConnectionFactory connectionFactory)
      : base(connectionFactory)
        {
        }
        public async Task<IEnumerable<SuppliersDTO>> GetAllAsync()
        {
            const string query = @"
                SELECT * form Supplier";

            return await Connection.QueryAsync<SuppliersDTO>(query);
        }


        public async Task<SuppliersDTO> GetByIdAsync(int id)
        {

            const string query = "SELECT * FROM Supplier WHERE Id = @Id";

            return await Connection.QuerySingleAsync<SuppliersDTO>(query, new { Id = id });
        }

        public async Task<int> CreateAsync(Suppliers supplier)
        {
            // Step 1: Get the last Code from the Suppliers table
            const string getLastCodeQuery = @"
        SELECT TOP 1 Code 
        FROM Suppliers 
        WHERE ISNUMERIC(Code) = 1
        ORDER BY TRY_CAST(Code AS INT) DESC";

            string lastCode = await Connection.QueryFirstOrDefaultAsync<string>(getLastCodeQuery);

            // Step 2: Calculate next code
            int nextCodeNumber = 1;
            if (!string.IsNullOrEmpty(lastCode) && int.TryParse(lastCode, out int parsedCode))
            {
                nextCodeNumber = parsedCode + 1;
            }

            // Step 3: Format to 4-digit string
            supplier.Code = nextCodeNumber.ToString("D4"); // e.g., "0001", "0045"

            // Step 4: Define the INSERT query
            const string insertQuery = @"
        INSERT INTO Suppliers (
            Name, Code, StatusId, LeadTime, TermsId, CurrencyId,
            TaxReg, IncotermsId, Contact, Email, Phone, Address,
            BankName, BankAcc, BankSwift, BankBranch,
            CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive
        )
        OUTPUT INSERTED.Id
        VALUES (
            @Name, @Code, @StatusId, @LeadTime, @TermsId, @CurrencyId,
            @TaxReg, @IncotermsId, @Contact, @Email, @Phone, @Address,
            @BankName, @BankAcc, @BankSwift, @BankBranch,
            @CreatedBy, @CreatedDate, @UpdatedBy, @UpdatedDate, @IsActive
        );";

            // Step 5: Execute insert
            return await Connection.QueryFirstAsync<int>(insertQuery, supplier);
        }




        public async Task UpdateAsync(Suppliers supplierDto)
        {
            const string query = @"
        UPDATE Supplier
        SET
            Name = @Name,
            ContactName = @ContactName,
            ContactTitle = @ContactTitle,
            CountryId = @CountryId,
            StateId = @StateId,
            CityId = @CityId,
            SupplierGroupId = @SupplierGroupId,
            RegionId = @RegionId,
            Address = @Address,
            PostalCode = @PostalCode,
            Phone = @Phone,
            Website = @Website,
            Fax = @Fax,
            Email = @Email,
            UpdatedBy = @UpdatedBy,
            UpdatedDate = @UpdatedDate,
            IsActive = @IsActive
        WHERE Id = @Id;
    ";

            await Connection.ExecuteAsync(query, supplierDto);
        }


        public async Task DeactivateAsync(int id)
        {
            const string query = "UPDATE Supplier SET IsActive = 0 WHERE ID = @id";
            await Connection.ExecuteAsync(query, new { ID = id });
        }


    }
}
