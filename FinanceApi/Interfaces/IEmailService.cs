using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IEmailService
    {
        Task SendResetPasswordEmail(User toEmail, string resetLink);
    }
}
