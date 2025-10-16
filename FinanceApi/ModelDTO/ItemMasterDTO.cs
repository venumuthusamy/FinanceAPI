namespace FinanceApi.ModelDTO
{
    public class ItemMasterDTO
    {
        public long Id { get; set; }
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Category { get; set; }
        public string? Uom { get; set; }
        public string? Barcode { get; set; }
        public long? CostingMethodId { get; set; }
        public long? TaxCodeId { get; set; }
        public string? Specs { get; set; }
        public string? PictureUrl { get; set; }
        public bool IsActive { get; set; } = true;

        // convenience from your left join
        public decimal OnHand { get; set; }
        public decimal Reserved { get; set; }
        public decimal Available { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }
}
