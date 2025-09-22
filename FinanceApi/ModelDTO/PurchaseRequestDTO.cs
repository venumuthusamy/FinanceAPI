﻿namespace FinanceApi.ModelDTO
{
    public class PurchaseRequestDTO
    {
        public int ID { get; set; }

        public string Requester { get; set; }

        public int DepartmentID { get; set; }

        public DateTime? DeliveryDate { get; set; }
        public string Description { get; set; }

        public bool? MultiLoc { get; set; }

        public bool? Oversea { get; set; }

        public string PRLines { get; set; }

        public DateTime? CreatedDate { get; set; }

        public DateTime? UpdatedDate { get; set; }

        public int CreatedBy { get; set; }

        public int UpdateddBy { get; set; }
        public string DepartmentName { get; set; }
        public string PurchaseRequestNo { get; set; }
    }
}
