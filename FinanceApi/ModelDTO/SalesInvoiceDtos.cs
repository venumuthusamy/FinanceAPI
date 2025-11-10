// ModelDTO/SalesInvoiceDtos.cs
namespace FinanceApi.ModelDTO
{
    public static class SalesInvoiceDtos
    {
        public record SiHeaderDto(
            int Id, string InvoiceNo, DateTime InvoiceDate,
            byte SourceType, int? SoId, int? DoId,
            int? CurrencyId, byte Status, bool IsActive
        );

        public record SiLineDto(
            int Id, int SiId, byte SourceType, int? SourceLineId,
            int ItemId, string ItemName, string? Uom,
            decimal Qty, decimal UnitPrice, decimal DiscountPct,
            int? TaxCodeId, int? CurrencyId
        );

        public record SiListRowDto(      // for list grid if needed
            int Id, string InvoiceNo, DateTime InvoiceDate, byte SourceType, string SourceRef, decimal Total
        );

        public class SiCreateRequest
        {
            public byte SourceType { get; set; }  // 1=SO, 2=DO
            public int? SoId { get; set; }
            public int? DoId { get; set; }
            public DateTime InvoiceDate { get; set; }
            public int? CurrencyId { get; set; }  // header default (optional)
            public List<SiCreateLine> Lines { get; set; } = new();
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
            public int? CurrencyId { get; set; }  // per-line currency
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
            public string TaxCodeId { get; set; }             // allow null
            public int? DefaultCurrencyId { get; set; }     // allow null
        }

    }
}
