namespace FinanceApi.ModelDTO
{
    public static class SalesInvoiceDtos
    {
        // MUST have parameterless ctor + settable props
        public class SiHeaderDto
        {
            public int Id { get; set; }
            public string InvoiceNo { get; set; } = "";
            public DateTime InvoiceDate { get; set; }
            public byte SourceType { get; set; }
            public int? SoId { get; set; }      // nullable
            public int? DoId { get; set; }      // nullable
            public byte Status { get; set; }
            public bool IsActive { get; set; }

            public SiHeaderDto() { } // important for Dapper
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
            public decimal Qty { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal DiscountPct { get; set; }
            public int? TaxCodeId { get; set; }

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
            public int? TaxCodeId { get; set; }

            public SiCreateLine() { }
        }

        public class SiCreateRequest
        {
            public byte SourceType { get; set; } // 1 or 2
            public int? SoId { get; set; }
            public int? DoId { get; set; }
            public DateTime InvoiceDate { get; set; }
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
            public int? DefaultCurrencyId { get; set; }     // allow null
        }
    }
}
