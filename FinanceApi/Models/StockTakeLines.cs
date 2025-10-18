namespace FinanceApi.Models
{
    public class StockTakeLines : BaseEntity
    {
        public int Id { get; set; }
        public int StockTakeId { get; set; }
        public int ItemId { get; set; }
        public decimal OnHand { get; set; }
        public decimal? CountedQty { get; set; }
        public decimal? BadCountedQty { get; set; }
        public decimal? VarianceQty { get; set; }
        public int ReasonId { get; set; }
        public string? Remarks { get; set; }
        public string? Barcode { get; set; }

        public bool Selected { get; set; }

    }
}
