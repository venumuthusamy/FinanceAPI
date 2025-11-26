// ModelDTO/ManualJournalCreateDto.cs
using System;

namespace FinanceApi.ModelDTO
{
    public class ManualJournalCreateDto
    {
        public int? AccountId { get; set; }
        public string? JournalDate { get; set; }          // from Angular (local date string e.g. "2025-11-20")
        public string? Type { get; set; }
        public int? CustomerId { get; set; }
        public int? SupplierId { get; set; }
        public string? Description { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public int? ItemId { get; set; }

        // Recurring flags
        public bool IsRecurring { get; set; }
        public string? RecurringFrequency { get; set; }   // Daily / Weekly / Monthly / Quarterly / Yearly / EveryMinute
        public int? RecurringInterval { get; set; }       // e.g. 10 => every 10 minutes

        public string? RecurringStartDate { get; set; }   // local date string from UI
        public string? RecurringEndType { get; set; }     // NoEnd / EndByDate / EndByCount
        public string? RecurringEndDate { get; set; }     // local date string
        public int? RecurringCount { get; set; }          // occurrences for EndByCount

        // Timezone (auto detected in Angular: Intl.DateTimeFormat().resolvedOptions().timeZone)
        public string? Timezone { get; set; }

        // These are computed in Controller (local → UTC)
        public DateTime? JournalDateUtc { get; set; }
        public DateTime? RecurringStartDateUtc { get; set; }
        public DateTime? RecurringEndDateUtc { get; set; }

        public int? CreatedBy { get; set; }

        public bool isPosted {  get; set; }
    }
}
