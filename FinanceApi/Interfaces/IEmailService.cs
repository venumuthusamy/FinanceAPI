namespace FinanceApi.Interfaces
{
    public interface IEmailService
    {
        Task SendResetPasswordEmail(string toEmail, string resetLink);
    }
}
