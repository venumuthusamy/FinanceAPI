namespace FinanceApi.ModelDTO
{
    public class EmailInvoiceListItemDto
    {
        public int Id { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public string PartyName { get; set; } = string.Empty; // Customer or Supplier
    }

    public class EmailInvoiceInfoDto
    {
        public int Id { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public string PartyName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? CcEmail { get; set; }
        public decimal Amount { get; set; }
    }

    public class EmailTemplateDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SubjectTemplate { get; set; } = string.Empty;
        public string BodyTemplate { get; set; } = string.Empty;
        public string? DocType { get; set; }  // optional: "SI" or "PIN" or null = both
    }

    public class EmailRequestDto
    {
        public string FromEmail { get; set; }   // 👈 logged-in user email
        public string FromName { get; set; }   // optional

        public string ToEmail { get; set; }   // 👈 recipient
        public string ToName { get; set; }   // optional

        public string Subject { get; set; }
        public string BodyHtml { get; set; }

        // for attachment file name, e.g. "INV-0001.pdf"
        public string FileName { get; set; }
    }

    public class EmailResultDto
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
    public class SmtpEmailSettings
    {
        public string From { get; set; } = string.Empty;      // default "from" (fallback)
        public string SmtpHost { get; set; } = string.Empty;
        public string SmtpPort { get; set; } = "587";
        public string SmtpUser { get; set; } = string.Empty;
        public string SmtpPass { get; set; } = string.Empty;
    }
}
