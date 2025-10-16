namespace FinanceApi.Models
{
    public class StockTakeLines : BaseEntity
    {
        public int Id { get; set; }
        public int StockTakeId { get; set; }
        public int ItemId { get; set; }
        public decimal AvailableQty { get; set; }
        public decimal? CountedQty { get; set; }
        public decimal? VarianceQty { get; set; }
        public string? Remarks { get; set; }
        public string? Barcode { get; set; }
    
    }
}
