namespace FinanceApi.ModelDTO
{
    public class ItemPriceDto
    {
        public long? Id { get; set; }
        public long SupplierId { get; set; }
        public decimal Price { get; set; }
    }
}
