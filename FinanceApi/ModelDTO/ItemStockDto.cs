﻿namespace FinanceApi.ModelDTO
{
    public class ItemStockDto
    {
        public long? Id { get; set; }
        public long WarehouseId { get; set; }
        public long? BinId { get; set; }
        public long? StrategyId { get; set; }
        public decimal OnHand { get; set; }
        public decimal Reserved { get; set; }
        public decimal? MinQty { get; set; }
        public decimal? MaxQty { get; set; }
        public decimal? ReorderQty { get; set; }
        public int? LeadTimeDays { get; set; }
        public bool BatchFlag { get; set; }
        public bool SerialFlag { get; set; }
    }
}
