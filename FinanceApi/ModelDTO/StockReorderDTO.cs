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
    }
}
