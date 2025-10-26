﻿namespace FinanceApi.Models
{
    public class StockTakeLines : BaseEntity
    {
        public int Id { get; set; }
        public int StockTakeId { get; set; }
        public int WarehouseTypeId { get; set; }     
        public int SupplierId { get; set; }
        public int Status { get; set; }
        public long ItemId { get; set; }
        public long BinId { get; set; }
        public decimal OnHand { get; set; }
        public decimal? CountedQty { get; set; }
        public decimal? BadCountedQty { get; set; }
        public decimal? VarianceQty { get; set; }
        public string? Reason { get; set; }
        public string? Remarks { get; set; }
        public string? Barcode { get; set; }

        public bool Selected { get; set; }

    }
}
