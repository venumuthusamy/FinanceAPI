namespace FinanceApi.ModelDTO
{
    
        public record ReorderSuggestionGroupDto(
            long SupplierId,
            long WarehouseId,
            List<ReorderSuggestionLineDto> Lines
        );
        public record ReorderSuggestionLineDto(long ItemId, decimal Qty, decimal Price);

    public record CreateReorderSuggestionsRequest(
        List<ReorderSuggestionGroupDto> Groups,
        string? Note,
        long UserId,
        string? UserName,
        long? DepartmentId
        );

    public sealed class CreatedPrDto
        {
            public int Id { get; set; }
            public string PurchaseRequestNo { get; set; } = "";
        }

    public sealed class ItemMeta
    {
        public long ItemId { get; set; }
        public string Sku { get; set; } = "";
        public string Name { get; set; } = "";
        public string Uom { get; set; } = "";
        public decimal? Budget { get; set; }
    }
}
