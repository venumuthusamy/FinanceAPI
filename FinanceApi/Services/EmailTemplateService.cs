using Azure.Identity;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;

namespace FinanceApi.Services
{
    public class EmailTemplateService : IEmailService
    {
        private readonly GraphServiceClient _graph;
        private readonly string _senderUserIdOrEmail;

        public EmailTemplateService(IConfiguration config)
        {
            var tenantId = config["AzureAd:TenantId"];
            var clientId = config["AzureAd:ClientId"];
            var clientSecret = config["AzureAd:ClientSecret"];
            _senderUserIdOrEmail = config["AzureAd:Sender"];   // e.g. accounts@fbh.com.sg

            // v5 style: TokenCredential + scopes
            var scopes = new[] { "https://graph.microsoft.com/.default" };

            var options = new TokenCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
            };

            var credential = new ClientSecretCredential(
                tenantId,
                clientId,
                clientSecret,
                options);

            _graph = new GraphServiceClient(credential, scopes);
        }

        public async Task<bool> SendInvoiceEmailAsync(EmailRequestDto dto, byte[] pdfBytes)
        {
            var message = new Message
            {
                Subject = dto.Subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Text,
                    Content = dto.Body
                },
                ToRecipients = new List<Recipient>
                {
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = dto.ToEmail
                        }
                    }
                }
            };

            if (!string.IsNullOrWhiteSpace(dto.CcEmail))
            {
                message.CcRecipients = new List<Recipient>
                {
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = dto.CcEmail!
                        }
                    }
                };
            }

            // Attach PDF (v5 style: just List<Attachment>)
            if (pdfBytes != null && pdfBytes.Length > 0)
            {
                message.Attachments = new List<Attachment>
                {
                    new FileAttachment
                    {
                        Name = $"{dto.InvoiceNo}.pdf",
                        ContentType = "application/pdf",
                        ContentBytes = pdfBytes
                    }
                };
            }

            // v5 style sendMail body
            var requestBody = new SendMailPostRequestBody
            {
                Message = message,
                SaveToSentItems = true
            };

            // v5 style send call
            await _graph.Users[_senderUserIdOrEmail]
                        .SendMail
                        .PostAsync(requestBody);

            return true;
        }
    }
}
