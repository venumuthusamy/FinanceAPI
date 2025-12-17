using FinanceApi.ModelDTO;
using FinanceApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OcrController : ControllerBase
{
    private readonly IOcrService _ocr;

    public OcrController(IOcrService ocr)
    {
        _ocr = ocr;
    }

    [HttpPost("extract")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<OcrResponseDto>> Extract(
     [FromForm] OcrExtractRequest req)
    {
        var createdBy = string.IsNullOrWhiteSpace(req.CreatedBy)
            ? "system"
            : req.CreatedBy;

        var result = await _ocr.ExtractAndStoreAsync(
            req.File,
            req.Lang,
            module: "PIN-DRAFT",
            refNo: null,
            createdBy
        );

        // ❌ NO SupplierInvoicePin insert here
        return Ok(result);
    }


    // ✅ Without GRN -> create Draft PIN
    [HttpPost("pin/create")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult> CreatePin([FromForm] OcrCreatePinRequest req)
    {
        var createdBy = string.IsNullOrWhiteSpace(req.CreatedBy) ? "system" : req.CreatedBy;

        var res = await _ocr.CreateDraftPinFromUploadAsync(req.File, req.Lang, req.CurrencyId, createdBy);

        return Ok(new { pinId = res.pinId, ocrId = res.ocrId });
    }
}
