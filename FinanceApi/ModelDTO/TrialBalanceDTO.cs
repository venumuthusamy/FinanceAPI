// ModelDTO/TB/TrialBalanceDTO.cs
namespace FinanceApi.ModelDTO.TB
{
    public class TrialBalanceDTO
    {
        public int HeadId { get; set; }
        public string HeadCode { get; set; }
        public string HeadName { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }
}
