using FinanceApi.Data;
using FinanceApi.ModelDTO;

using FinanceApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;
using Tesseract;

namespace FinanceApi.Services;

public interface IOcrService
{
    Task<OcrResponseDto> ExtractAndStoreAsync(IFormFile file, string? lang, string? module, string? refNo, string createdBy);
    Task<(int pinId, int ocrId)> CreateDraftPinFromUploadAsync(IFormFile file, string? lang, int? currencyId, string createdBy);
}

public class OcrService : IOcrService
{
    private readonly IWebHostEnvironment _env;
    private readonly ApplicationDbContext _db;

    public OcrService(IWebHostEnvironment env, ApplicationDbContext db)
    {
        _env = env;
        _db = db;
    }

    public async Task<OcrResponseDto> ExtractAndStoreAsync(IFormFile file, string? lang, string? module, string? refNo, string createdBy)
    {
        var (filePath, contentType) = await SaveUploadAsync(file);

        EnsureImage(contentType);

        var ocr = await RunTesseractAsync(filePath, string.IsNullOrWhiteSpace(lang) ? "eng" : lang!.Trim());

        var parsed = ParseInvoiceFields(ocr.Text);
        var parsedJson = JsonSerializer.Serialize(parsed);

        var doc = new OcrDocument
        {
            Module = module,
            RefNo = refNo,
            FileName = file.FileName,
            ContentType = contentType,
            FilePath = filePath,
            ExtractedText = ocr.Text,
            ParsedJson = parsedJson,
            MeanConfidence = (decimal)ocr.MeanConfidence, // ✅ decimal in DB
            CreatedBy = createdBy
        };

        _db.OcrDocuments.Add(doc);
        await _db.SaveChangesAsync();

        return new OcrResponseDto
        {
            OcrId = doc.Id,
            Text = ocr.Text,
            MeanConfidence = ocr.MeanConfidence,
            WordCount = ocr.WordCount,
            Parsed = parsed
        };
    }

    // ✅ OPTION-2: No GRN -> create Draft PIN directly
    public async Task<(int pinId, int ocrId)> CreateDraftPinFromUploadAsync(IFormFile file, string? lang, int? currencyId, string createdBy)
    {
        // 1) Run OCR + store OCR log
        var resp = await ExtractAndStoreAsync(file, lang, module: "PIN", refNo: null, createdBy: createdBy);

        // 2) Create SupplierInvoicePin draft
        // NOTE: LinesJson empty for now; later you can parse items & build lines
        var invNo = string.IsNullOrWhiteSpace(resp.Parsed.InvoiceNo) ? "PIN-DRAFT" : resp.Parsed.InvoiceNo!.Trim();
        var invDate = ParseDate(resp.Parsed.InvoiceDate) ?? DateTime.Today;

        var amount = resp.Parsed.Total ?? 0m;
        var taxAmount = resp.Parsed.TaxAmount ?? 0m;

        var pin = new SupplierInvoicePin
        {
            InvoiceNo = invNo,
            InvoiceDate = invDate,
            Amount = amount,
            Tax = taxAmount,
            CurrencyId = currencyId ?? 1,
            Status = 1, // ✅ Draft/Hold
            LinesJson = "[]",
            IsActive = true,
            CreatedDate = DateTime.Now,
            UpdatedDate = DateTime.Now,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            GrnId = null,
            SupplierId = null
        };

        _db.SupplierInvoicePin.Add(pin);
        await _db.SaveChangesAsync();

        return (pin.Id, resp.OcrId);
    }

    // ---------------- helpers ----------------

    private async Task<(string filePath, string contentType)> SaveUploadAsync(IFormFile file)
    {
        var uploads = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "ocr");
        Directory.CreateDirectory(uploads);

        var safeName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
        var filePath = Path.Combine(uploads, safeName);

        await using var fs = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(fs);

        var contentType = (file.ContentType ?? "").ToLowerInvariant();
        return (filePath, contentType);
    }

    private static void EnsureImage(string contentType)
    {
        var allowed = new HashSet<string> { "image/png", "image/jpeg", "image/jpg", "image/webp" };
        if (!allowed.Contains(contentType))
            throw new InvalidOperationException("Only image files supported. (PDF needs PDF->image conversion.)");
    }

    public async Task<OcrTextResult> RunTesseractAsync(string imagePath, string lang)
    {
        // ✅ runtime base: bin\x64\Debug\net8.0\
        var baseDir = AppContext.BaseDirectory;

        // ✅ IMPORTANT: engine needs tessdata folder path
        var tessdataDir = Path.Combine(baseDir, "OCR", "tessdata");

        if (!Directory.Exists(tessdataDir))
            throw new Exception($"tessdata folder not found: {tessdataDir}");

        // ✅ check required traineddata files
        foreach (var l in (lang ?? "eng").Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var trained = Path.Combine(tessdataDir, $"{l}.traineddata");
            if (!File.Exists(trained))
                throw new Exception($"Missing traineddata: {trained}");
        }

        return await Task.Run(() =>
        {
            try
            {
                // EngineMode.Default is safest
                using var engine = new TesseractEngine(tessdataDir, lang, EngineMode.Default);

                using var img = Pix.LoadFromFile(imagePath);
                using var page = engine.Process(img);

                var text = page.GetText() ?? "";
                var mean = (float)page.GetMeanConfidence(); // float already
                var words = string.IsNullOrWhiteSpace(text)
                    ? 0
                    : text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

                return new OcrTextResult
                {
                    Text = text.Trim(),
                    MeanConfidence = mean,
                    WordCount = words
                };
            }
            catch (Exception ex)
            {
                // very clear debug message
                throw new Exception(
                    $"Tesseract init failed.\n" +
                    $"BaseDir={baseDir}\n" +
                    $"TessdataDir={tessdataDir}\n" +
                    $"Lang={lang}\n" +
                    $"Image={imagePath}\n" +
                    $"Inner={ex.Message}", ex);
            }
        });
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return Regex.Matches(text.Trim(), @"\S+").Count;
    }

    private static DateTime? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s, out var d)) return d.Date;

        // if OCR gives dd/MM/yyyy
        if (DateTime.TryParseExact(s, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var d2))
            return d2.Date;

        // if OCR gives yyyy-MM-dd
        if (DateTime.TryParseExact(s, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var d3))
            return d3.Date;

        return null;
    }

    // very basic parsing (improve later)
    private static OcrParsedDto ParseInvoiceFields(string text)
    {
        var dto = new OcrParsedDto();

        // Invoice No
        dto.InvoiceNo = Regex.Match(text, @"Invoice\s*No\s*[:\-]?\s*(.+)", RegexOptions.IgnoreCase)
            .Groups.Count > 1 ? Regex.Match(text, @"Invoice\s*No\s*[:\-]?\s*(.+)", RegexOptions.IgnoreCase).Groups[1].Value.Trim() : null;

        // Invoice Date
        var mDate = Regex.Match(text, @"Invoice\s*Date\s*[:\-]?\s*([0-9]{1,2}[/\-][0-9]{1,2}[/\-][0-9]{2,4})", RegexOptions.IgnoreCase);
        if (mDate.Success) dto.InvoiceDate = mDate.Groups[1].Value.Trim();

        // Grand Total
        var mTotal = Regex.Match(text, @"GRAND\s*TOTAL\s*[:\-]?\s*([0-9,]+(\.[0-9]{1,2})?)", RegexOptions.IgnoreCase);
        if (mTotal.Success) dto.Total = ToDecimal(mTotal.Groups[1].Value);

        // Tax % (GST 18%)
        var mTaxPct = Regex.Match(text, @"GST\s*([0-9]{1,2}(\.[0-9]{1,2})?)\s*%", RegexOptions.IgnoreCase);
        if (mTaxPct.Success) dto.TaxPercent = ToDecimal(mTaxPct.Groups[1].Value);

        // Tax Amount
        var mTaxAmt = Regex.Match(text, @"Tax.*[:\-]?\s*([0-9,]+(\.[0-9]{1,2})?)", RegexOptions.IgnoreCase);
        if (mTaxAmt.Success) dto.TaxAmount = ToDecimal(mTaxAmt.Groups[1].Value);

        return dto;
    }

    private static decimal? ToDecimal(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Replace(",", "").Trim();
        return decimal.TryParse(s, out var d) ? d : null;
    }
}
