namespace FinanceApi.ModelDTO
{
    public class ManualJournalCreateDto
    {
        public int? AccountId { get; set; }
        public DateTime? JournalDate { get; set; }

        public string? Type { get; set; } // "Customer" / "Supplier" / null
        public int? CustomerId { get; set; }
        public int? SupplierId { get; set; }

        public string? Description { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }

        // Recurring
        public bool IsRecurring { get; set; }
        public string? RecurringFrequency { get; set; }  // Daily/Weekly/Monthly/Yearly
        public int? RecurringInterval { get; set; }      // Every X
        public DateTime? RecurringStartDate { get; set; }
        public string? RecurringEndType { get; set; }    // NoEnd/EndByDate/EndByCount
        public DateTime? RecurringEndDate { get; set; }
        public int? RecurringCount { get; set; }

        public int? CreatedBy { get; set; }
    }
}
