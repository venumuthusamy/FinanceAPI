namespace FinanceApi.ModelDTO
{
    public class JournalsDTO
    {
        public int Id { get; set; }
        public string JournalNo { get; set; } = string.Empty;
        public DateTime JournalDate { get; set; }

        public decimal Amount { get; set; }       // Credit
        public decimal DebitAmount { get; set; }  // Debit

        public bool IsRecurring { get; set; }
        public string? RecurringFrequency { get; set; }

        public bool IsPosted { get; set; }

        public string HeadName { get; set; } = string.Empty;
        public string HeadCode { get; set; } = string.Empty;
    }
}
