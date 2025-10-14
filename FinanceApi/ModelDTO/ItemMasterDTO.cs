namespace FinanceApi.ModelDTO
{
    public class ItemMasterDTO
    {
        public int Id { get; set; }
        public string Sku { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string? Category { get; set; }
        public string? Uom { get; set; }
        public string WareHouse { get; set; }
        public string? Barcode { get; set; }
        public string? Costing { get; set; }
        public decimal? MinQty { get; set; }
        public decimal? MaxQty { get; set; }
        public decimal? ReorderQty { get; set; }
        public int? LeadTimeDays { get; set; }
        public bool BatchFlag { get; set; }
        public bool SerialFlag { get; set; }
        public string? TaxClass { get; set; }
        public string? Specs { get; set; }
        public string? PictureUrl { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }

        // inventory snapshot for list/detail
        public decimal OnHand { get; set; }
        public decimal Reserved { get; set; }
        public decimal Available { get; set; }
    }
}
