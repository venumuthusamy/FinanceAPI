using FinanceApi.Models;

namespace FinanceApi.ModelDTO
{
    public class PickingDTO : BaseEntity
    {
        public int Id { get; set; }
        public int SoId { get; set; }
        public DateTime? SoDate { get; set; }
        public string? BarCode { get; set; }
        public string? QrCode { get; set; }
        public byte[]? BarCodeSrc { get; set; }
        public byte[]? QrCodeSrc { get; set; }
        public byte Status { get; set; }

        public List<PickingLineDTO> LineItems { get; set; } = new();
    }

    public class PickingLineDTO : BaseEntity
    {
        public int Id { get; set; }
        public int PickId { get; set; }
        public int SoLineId { get; set; }
        public long ItemId { get; set; }
        public int WarehouseId { get; set; }
        public int? SupplierId { get; set; }
        public int? BinId { get; set; }
        public decimal DeliverQty { get; set; }
        public int? CartonId { get; set; }
    }
}
