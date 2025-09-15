using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceApi.Models
{
    public enum AccountType
    {
        Debit = 1,
        Credit = 2
    }
    public class OpeningBalance : BaseEntity
    {
        public int Id { get; set; }       
        public DateTime Date { get; set; }
        public int AccountHeadId { get; set; }

        [ForeignKey("AccountHeadId")]
        public ChartOfAccount? ChartOfAccount { get; set; }       
        public AccountType? BalanceType { get; set; }
        public decimal Amount { get; set; }
        public string Remark { get; set; }
    }

    public class OpeningBalanceDto

    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public int AccountHeadId { get; set; }
        public string? AccountHeadName { get; set; }
        public AccountType? BalanceType { get; set; }
        public string? BalanceTypeName => BalanceType?.ToString();
        public decimal Amount { get; set; }
        public string Remark { get; set; }

        public string? CreatedBy { get; set; }       
        public DateTime? CreatedDate { get; set; }
        public string? UpdatedBy { get; set; }        
        public DateTime? UpdatedDate { get; set; }
        public bool IsActive { get; set; }
    }
}
