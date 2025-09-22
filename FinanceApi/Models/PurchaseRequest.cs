﻿using System.ComponentModel.DataAnnotations.Schema;

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

        public int CreatedBy { get; set; }

        public int UpdateddBy { get; set; }
        public string PurchaseRequestNo { get; set; }   
    }
}
