namespace FinanceApi.Models
{
    public class SalesOrderLines : BaseEntity
    {
        public int Id { get; set; }
        public int SalesOrederId { get; set; }
        public int ItemId { get; set; }
        public string? ItemName { get; set; }
        public int Uom { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }      
        public string? Tax { get; set; }
        public decimal Total { get; set; }
    }
}
