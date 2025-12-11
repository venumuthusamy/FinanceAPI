// ModelDTO/SalesInvoiceDtos.cs
namespace FinanceApi.ModelDTO
{
    public static class SalesInvoiceDtos
    {
        public class SiHeaderDto
        {
            public int Id { get; set; }
            public string InvoiceNo { get; set; } = "";
            public DateTime InvoiceDate { get; set; }
            public byte SourceType { get; set; }
            public int? SoId { get; set; }
            public decimal TaxAmount { get; set; }
            public int? DoId { get; set; }
            public decimal Total { get; set; }
            public byte Status { get; set; }
            public bool IsActive { get; set; }
            public SiHeaderDto() { }
        }

        public class SiLineDto
        {
            public int Id { get; set; }
            public int SiId { get; set; }
            public byte SourceType { get; set; }
            public int? SourceLineId { get; set; }
            public int? ItemId { get; set; }
            public string? ItemName { get; set; }
            public string? Uom { get; set; }
            public decimal TaxAmount { get; set; }
            public decimal Qty { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal DiscountPct { get; set; }
            public decimal GstPct { get; set; }
            public string? Tax { get; set; }
            public int? TaxCodeId { get; set; }
            public string? Description { get; set; }   // <— NEW
            public SiLineDto() { }
        }

        public class SiListRowDto
        {
            public int Id { get; set; }
            public string InvoiceNo { get; set; } = "";
            public DateTime InvoiceDate { get; set; }
            public byte SourceType { get; set; }
            public string SourceRef { get; set; } = "";
            public decimal Total { get; set; }
            public SiListRowDto() { }
        }

        public class SiCreateLine
        {
            public int? SourceLineId { get; set; }
            public int ItemId { get; set; }
            public string? ItemName { get; set; }
            public string? Uom { get; set; }
            public decimal Qty { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal DiscountPct { get; set; }
            public decimal GstPct { get; set; }
            public decimal TaxAmount { get; set; }
            public string? Tax { get; set; }
            public int? TaxCodeId { get; set; }
            
            public decimal LineAmount { get; set; }            
            public string? Description { get; set; }   // <— NEW
            public int? BudgetLineId { get; set; }
            public SiCreateLine() { }
        }

        public class SiCreateRequest
        {
            public byte SourceType { get; set; } // 1 or 2
            public int? SoId { get; set; }
            public int? DoId { get; set; }
            public DateTime InvoiceDate { get; set; }
            public decimal? Total { get; set; }
            public decimal Subtotal { get; set; }
            public decimal ShippingCost { get; set; }
            public string Remarks { get; set; }
            public int? AdvanceId { get; set; }
            public decimal TaxAmount { get; set; }
            public decimal? AdvanceApplyAmount { get; set; }

            public List<SiCreateLine> Lines { get; set; } = new();

        }

        public class SiSourceLineDto
        {
            public int SourceLineId { get; set; }
            public byte SourceType { get; set; }
            public int SourceId { get; set; }
            public int ItemId { get; set; }
            public string ItemName { get; set; } = "";
            public string? UomName { get; set; }
            public decimal QtyOpen { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal DiscountPct { get; set; }
            public decimal GstPct { get; set; }
            public string? Tax { get; set; }
            // DefaultCurrencyId intentionally removed
        }
    }
}
