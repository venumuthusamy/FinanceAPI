using FinanceApi.Models;

namespace FinanceApi.ModelDTO
{
    public class StockReorderDTO : BaseEntity
    {
        public int Id { get; set; }
        public int WarehouseTypeId { get; set; }
        public string? WarehouseName { get; set; }
        public int MethodId { get; set; }
        public int Horizon { get; set; }
        public bool LeadTime { get; set; }
        //public int Status { get; set; }
        public List<StockReorderLines> LineItems { get; set; } = new();

        public StockReorderStatus Status { get; set; } = StockReorderStatus.Draft;
    }
    public enum StockReorderStatus
    {
        Draft = 1,
        Approved = 2,
        Posted = 3
    }

    public enum StockReorderLineStatus { Draft = 1, Approved = 2, Posted = 3 }
}
