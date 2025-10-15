namespace FinanceApi.Models
{
    public class Stock
    {
        public int ID { get; set; }
        public int ItemID { get; set; }

        public int WareHouseID { get; set; }
        public int Available { get; set; }
        public int OnHand { get; set; }
        public int Reserved { get; set; }
        public int Min { get; set; }
        public int Expiry { get; set; }
        public int isTransfer { get; set; }

        public long CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }

        public long UpdatedBy { get; set; }
        public DateTime UpdatedDate { get; set; }
    }
}
