namespace FinanceApi.ModelDTO
{
    public class JournalsDTO
    {
        public string HeadName { get; set; }

        public long Amount { get; set; }
        public long DebitAmount { get; set; }

        public int HeadCode { get; set; }

        public string RowType { get; set; }
    }
}
