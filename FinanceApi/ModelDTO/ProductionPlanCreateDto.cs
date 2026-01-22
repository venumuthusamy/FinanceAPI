namespace FinanceApi.ModelDTO
{
    public class SoHeaderDto
    {
        public int Id { get; set; }
        public string SalesOrderNo { get; set; } = "";
        public int? CustomerId { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public string? Status { get; set; }
    }

    public class PlanRowDto
    {
        public int RecipeId { get; set; }
        public int FinishedItemId { get; set; }
        public string RecipeName { get; set; } = "";
        public decimal PlannedQty { get; set; }
        public decimal ExpectedOutput { get; set; }
        public decimal BatchQty { get; set; }
        public decimal HeaderYieldPct { get; set; }
    }

    public class IngredientRowDto
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = "";
        public string? Uom { get; set; }
        public decimal RequiredQty { get; set; }
        public decimal AvailableQty { get; set; }
        public string Status { get; set; } = "OK";
    }

    public class ProductionPlanResponseDto
    {
        public List<PlanRowDto> PlanRows { get; set; } = new();
        public List<IngredientRowDto> Ingredients { get; set; } = new();
    }

    public class SavePlanRequest
    {
        public int SalesOrderId { get; set; }
        public int? OutletId { get; set; }
        public int? WarehouseId { get; set; }
        public string? CreatedBy { get; set; }
    }

}
