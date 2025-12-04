namespace FinanceApi.Models
{
    public class ManualJournalDto
    {
        public int Id { get; set; }

        public string? JournalNo { get; set; }   // <== NEW

        public int AccountId { get; set; }
        public string? AccountName { get; set; }

        public DateTime JournalDate { get; set; }
        public string? Type { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public int? ItemId { get; set; }

        public string? Description { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }

        public int? BudgetLineId { get; set; }
        

        public bool IsRecurring { get; set; }
        public string? RecurringFrequency { get; set; }
        public int? RecurringInterval { get; set; }
        public DateTime? RecurringStartDate { get; set; }
        public string? RecurringEndType { get; set; }
        public DateTime? RecurringEndDate { get; set; }
        public int? RecurringCount { get; set; }
        public int ProcessedCount { get; set; }
        public DateTime? NextRunDate { get; set; }
    }

}
