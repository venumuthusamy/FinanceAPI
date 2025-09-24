using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceApi.Models
{
    [Table("PurchaseGoodReceipt")]
    public class PurchaseGoodReceiptItems
    {

        public int ID { get; set; }
        public int POID { get; set; }

        public DateTime ReceptionDate { get; set; }

        public int OverReceiptTolerance { get; set; }

        public string GRNJson { get; set; }
    }
}
