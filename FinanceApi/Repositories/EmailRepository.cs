using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using FinanceApi.Repositories;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

public class EmailRepository : DynamicRepository, IEmailRepository
{
    public EmailRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
    }

    public async Task<EmailTemplate> GetTemplateAsync(int id)
    {
        const string sql = @"SELECT * FROM EmailTemplate WHERE Id = @Id AND IsActive = 1";
        return await Connection.QueryFirstOrDefaultAsync<EmailTemplate>(sql, new { Id = id });
    }
}
