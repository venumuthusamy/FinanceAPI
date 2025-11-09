namespace FinanceApi.Models
{
    public class SalesOrder : BaseEntity
    {
        public int Id { get; set; }
        public int QuotationNo { get; set; }
        public int CustomerId { get; set; }
        public int WarehouseId { get; set; }
        public DateTime RequestedDate { get; set; }
        public DateTime DeliveryDate { get; set; }
        public int? Status { get; set; }
        public int? Shipping { get; set; }
        public int? Discount { get; set; }
        public int GstPct { get; set; }
        public List<SalesOrderLines> LineItems { get; set; } = new();
      
    }
}
