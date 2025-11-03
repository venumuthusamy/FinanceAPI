using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceApi.Models
{
    [Table("PurchaseRequest")]
    public class PurchaseRequest
    {
        public int ID { get; set; }

        public string Requester { get; set; }

        public int DepartmentID { get; set; }

        public DateTime? DeliveryDate { get; set; }
        public string Description {  get; set; }

        public bool? MultiLoc { get; set; }

        public bool? Oversea { get; set; }

        public string PRLines { get; set; }

        public DateTime? CreatedDate { get; set; }

        public DateTime? UpdatedDate { get; set; }

        public string CreatedBy { get; set; }

        public string UpdatedBy { get; set; }
        public string PurchaseRequestNo { get; set; }
        public bool IsActive { get; set; }
        public int Status {  get; set; }
        public bool IsReorder { get; set; }
        public long? StockReorderId { get; set; }
    }
}
