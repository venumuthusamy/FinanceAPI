using FinanceApi.Models;

namespace FinanceApi.ModelDTO
{
    public class SalesOrderDTO : BaseEntity
    {
        public int Id { get; set; }
        public string SalesOrderNo { get; set; }
        public int QuotationNo { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public int WarehouseId { get; set; }
        public string WarehouseName { get; set; }
        public DateTime RequestedDate { get; set; }
        public DateTime DeliveryDate { get; set; }
        public int? Status { get; set; }
        public int? Shipping { get; set; }
        public int? Discount { get; set; }
        public int GstPct { get; set; }
        public List<SalesOrderLines> LineItems { get; set; } = new();
        public List<SalesOrderLinesList> LineItemsList { get; set; } = new();
    }
    public class SalesOrderLinesList : BaseEntity
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
        public string UomName { get; set; }
    }
}
