namespace FinanceApi.ModelDTO
{
    public class GeneralLedgerDTO
    {
        public int HeadId { get; set; }
        public int HeadCode { get; set; }
        public string HeadName { get; set; }
        public int ParentHead { get; set; }

        public decimal OpeningBalance { get; set; }

        public decimal Received { get; set; }
        public decimal Balance { get; set; }
    }
}
