using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IEmailRepository
    {
        Task<EmailTemplate?> GetTemplateAsync(int id);
    }
}
