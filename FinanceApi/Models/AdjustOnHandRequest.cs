namespace FinanceApi.Models
{
    public class AdjustOnHandRequest
    {
        public long ItemId { get; set; }
        public long WarehouseId { get; set; }
        public long? BinId { get; set; }            // nullable match
        public long? SupplierId { get; set; }       // optional, if you track price per supplier
        public decimal NewOnHand { get; set; }
        public long? StockIssueId { get; set; }
        public string? UpdatedBy { get; set; }
    }
    public sealed class AdjustOnHandResult
    {
        public decimal OnHand { get; set; }
        public decimal Reserved { get; set; }
        public decimal Available { get; set; }
        public decimal? PriceQty { get; set; }      // optional echo of ItemPrice.Qty after update
    }
}
