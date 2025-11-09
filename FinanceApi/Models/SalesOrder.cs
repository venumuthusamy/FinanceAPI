namespace FinanceApi.Models
{
       public class SalesOrder
    {
        public int Id { get; set; }
        public int QuotationNo { get; set; }
        public int CustomerId { get; set; }
        public DateTime? RequestedDate { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public byte Status { get; set; } = 1;
        public decimal Shipping { get; set; }
        public decimal Discount { get; set; }
        public decimal GstPct { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public int? UpdatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public bool IsActive { get; set; } = true;

        public List<SalesOrderLines> LineItems { get; set; } = new();
    }

        //public List<SalesOrderLines> LineItems { get; set; } = new();
      
    }

