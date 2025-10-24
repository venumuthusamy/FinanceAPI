namespace FinanceApi.ModelDTO
{
    public sealed class BomLatestRow
    {
        public int SupplierId { get; set; }
        public decimal ExistingCost { get; set; }
        public decimal? UnitCost { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public sealed class BomHistoryPoint
    {
        public int SupplierId { get; set; }
        public decimal ExistingCost { get; set; }
        public decimal? UnitCost { get; set; }
        public DateTime CreatedDate { get; set; }
        public int Rn { get; set; }  // 1..3
    }

    public sealed class BomSnapshot
    {
        public IReadOnlyList<BomLatestRow> Latest { get; init; } = Array.Empty<BomLatestRow>();
        public IReadOnlyList<BomHistoryPoint> History { get; init; } = Array.Empty<BomHistoryPoint>();
    }

}
