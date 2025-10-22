﻿namespace FinanceApi.Models
{
    public class StockTakeWarehouseItem : BaseEntity
    {
        public long ItemId { get; set; }
        public string Sku { get; set; } = "";
        public string ItemName { get; set; } = "";
        public string Barcode { get; set; } = "";
        public long WarehouseId { get; set; }
        public long? BinId { get; set; }
        public long SupplierId { get; set; }
        public decimal OnHand { get; set; }
        public decimal Reserved { get; set; }
        public decimal AvailableQty { get; set; }
        public decimal? MinQty { get; set; }
        public decimal? MaxQty { get; set; }
        public decimal? ReorderQty { get; set; }
    }
}
