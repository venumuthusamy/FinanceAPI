// File: ModelDTO/DeliveryOrderDtos.cs
namespace FinanceApi.ModelDTO
{
    public static class DeliveryOrderDtos
    {
        public record DoHeaderDto(
            int Id, string DoNumber, int Status,
            int? SoId, int? PackId,
            int? DriverId, int? VehicleId,
            string? RouteName, DateTime? DeliveryDate,
            string? PodFileUrl, bool IsPosted,
            string SalesOrderNo
        );

        public record DoLineDto(
            int Id, int DoId, int? SoLineId, int? PackLineId,
            int? ItemId, string ItemName, string? Uom, decimal Qty, string? Notes
        );

        public class DoCreateRequest
        {
            public int? SoId { get; set; }
            public int? PackId { get; set; }
            public int DriverId { get; set; }             // NOT NULL in DB
            public int? VehicleId { get; set; }
            public string? RouteName { get; set; }        // free text
            public DateTime? DeliveryDate { get; set; }   // date only or datetime
            public List<DoCreateLine> Lines { get; set; } = new();

            public class DoCreateLine
            {
                public int? SoLineId { get; set; }
                public int? PackLineId { get; set; }
                public int? ItemId { get; set; }
                public string ItemName { get; set; } = "";
                public string? Uom { get; set; }
                public decimal Qty { get; set; }
                public string? Notes { get; set; }
            }
        }

        public class DoUpdateHeaderRequest
        {
            public int? DriverId { get; set; }
            public int? VehicleId { get; set; }
            public string? RouteName { get; set; }
            public DateTime? DeliveryDate { get; set; }
        }

        public class DoAddLineRequest
        {
            public int DoId { get; set; }
            public int? SoLineId { get; set; }
            public int? PackLineId { get; set; }
            public int? ItemId { get; set; }
            public string ItemName { get; set; } = "";
            public string? Uom { get; set; }
            public decimal Qty { get; set; }
            public string? Notes { get; set; }
        }
    }
}
