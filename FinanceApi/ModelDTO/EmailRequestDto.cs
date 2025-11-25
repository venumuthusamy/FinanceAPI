namespace FinanceApi.ModelDTO
{
    public class EmailRequestDto
    {
        public string InvoiceNo { get; set; }
        public string ToEmail { get; set; }
        public string CcEmail { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public int TemplateId { get; set; }
    }
    public class EmailResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

}
