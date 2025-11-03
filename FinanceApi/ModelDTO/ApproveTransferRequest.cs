namespace FinanceApi.ModelDTO
{
    public class ApproveTransferRequest
    {
        public int StockId { get; set; }              // ✅ Stock.Id
        public int ItemId { get; set; }          // ✅ Item.Id
        public int WarehouseId { get; set; }
        public int? BinId { get; set; }
        public int ToWarehouseId { get; set; }
        public int ToBinId { get; set; }
        public decimal TransferQty { get; set; }
        public string? Remarks { get; set; }
        public bool IsFullTransfer { get; set; }
        public bool IsPartialTransfer { get; set; }
        public int? SupplierId { get; set; }
    }
}
