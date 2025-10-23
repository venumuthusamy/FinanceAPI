namespace FinanceApi.ModelDTO
{
    public class ItemMasterUpsertDto
    {
        public long Id { get; set; }              // 0 for create
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Category { get; set; }
        public string? Uom { get; set; }
       
        public long? CostingMethodId { get; set; }
        public long? TaxCodeId { get; set; }
        public string? Specs { get; set; }
        public string? PictureUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public DateTime ExpiryDate { get; set; }

        public List<ItemPriceDto> Prices { get; set; } = new();
        public List<ItemStockDto> ItemStocks { get; set; } = new();
        public List<ItemBomUpsertDto>? BomLines { get; set; }
    }
}
