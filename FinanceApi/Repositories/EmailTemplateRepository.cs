using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.Models;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace FinanceApi.Repositories
{
    public class EmailTemplateRepository: DynamicRepository,IEmailRepository
    {
        public EmailTemplateRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        public async Task<EmailTemplate?> GetTemplateAsync(int id)
        {
            const string sql = @"
SELECT Id, TemplateName, SubjectTemplate, BodyTemplate, IsActive
FROM dbo.EmailTemplate
WHERE Id = @Id AND IsActive = 1;";

            return await Connection.QueryFirstOrDefaultAsync<EmailTemplate>(sql, new { Id = id });
        }
    }
}
