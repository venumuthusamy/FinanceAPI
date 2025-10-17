namespace FinanceApi.Models
{
    public class AdjustOnHandRequest
    {
        public long ItemId { get; set; }
        public long WarehouseId { get; set; }
        public long? BinId { get; set; }
        public decimal NewOnHand { get; set; }
        public long? StockIssueID { get; set; }
    }
}
