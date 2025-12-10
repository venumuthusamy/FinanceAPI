namespace FinanceApi.ModelDTO
{
    public class ApSupplierAdvanceDto
    {
        public int Id { get; set; }
        public string AdvanceNo { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }
        public DateTime AdvanceDate { get; set; }

        public decimal OriginalAmount { get; set; }
        public decimal UtilisedAmount { get; set; }
        public decimal BalanceAmount { get; set; }
    }
    public class ApSupplierAdvanceCreateRequest
    {
        public int SupplierId { get; set; }
        public DateTime AdvanceDate { get; set; }
        public decimal Amount { get; set; }

        public string ReferenceNo { get; set; }
        public string Notes { get; set; }

        public int MethodId { get; set; }      // Cash / Bank / Cheque / Other
        public int? BankHeadId { get; set; }   // COA Id for bank account (nullable)
    }
}
