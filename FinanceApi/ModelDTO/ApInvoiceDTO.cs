namespace FinanceApi.ModelDTO
{
    public class ApInvoiceDTO
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }
        public string InvoiceNo { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }

        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }

        public decimal DebitNoteAmount { get; set; }
        public string DebitNoteNo { get; set; }
        public DateTime? DebitNoteDate { get; set; }

        public decimal OutstandingAmount { get; set; }
        public byte Status { get; set; }
    }

}
