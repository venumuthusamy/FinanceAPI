using FinanceApi.ModelDTO;
using ImageMagick;
using Tesseract;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Page = UglyToad.PdfPig.Content.Page;

namespace FinanceApi.Services
{
    public interface IOcrService
    {
        Task<OcrResponseDto> ExtractAnyAsync(IFormFile file, string lang, string? module, string? refNo, string? createdBy);
    }
    public class OcrService:IOcrService
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _cfg;

        public OcrService(IWebHostEnvironment env, IConfiguration cfg)
        {
            _env = env;
            _cfg = cfg;
        }

        public async Task<OcrResponseDto> ExtractAnyAsync(IFormFile file, string lang, string? module, string? refNo, string? createdBy)
        {
            if (file == null || file.Length == 0) throw new InvalidOperationException("File is empty");

            // save original
            var uploads = Path.Combine(_env.ContentRootPath, "Uploads", "OCR");
            Directory.CreateDirectory(uploads);

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var safeName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
            var savedPath = Path.Combine(uploads, safeName);

            await using (var fs = new FileStream(savedPath, FileMode.Create))
                await file.CopyToAsync(fs);

            string extractedText = "";
            float conf = 0;
            int wordCount = 0;

            if (ext == ".pdf" || file.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
            {
                // 1) Try digital PDF text first
                extractedText = ExtractPdfText(savedPath);

                if (CountWords(extractedText) < 20) // scanned pdf likely
                {
                    var dpi = _cfg.GetValue<int?>("Ocr:PdfDpi") ?? 300;
                    var pngs = RenderPdfToPngs(savedPath, dpi);
                    var ocr = RunTesseractOnImages(pngs, lang);
                    extractedText = ocr.Text;
                    conf = ocr.MeanConfidence;
                    wordCount = ocr.WordCount;

                    // cleanup temp pngs
                    foreach (var p in pngs) TryDelete(p);
                }
                else
                {
                    wordCount = CountWords(extractedText);
                    conf = 0.99f; // digital text (not OCR)
                }
            }
            else
            {
                // image
                var ocr = RunTesseractOnImage(savedPath, lang);
                extractedText = ocr.Text;
                conf = ocr.MeanConfidence;
                wordCount = ocr.WordCount;
            }

            var parsed = ParseInvoice(extractedText);

            // NOTE: Here you can store OCR doc to DB and set OcrId from DB.
            // For now return dummy id
            return new OcrResponseDto
            {
                OcrId = 1,
                Text = extractedText,
                MeanConfidence = conf,
                WordCount = wordCount,
                Parsed = parsed
            };
        }

        // ---------------- PDF TEXT (Digital) ----------------
        private string ExtractPdfText(string pdfPath)
        {
            var sb = new StringBuilder();
            using var document = PdfDocument.Open(pdfPath);
            foreach (Page page in document.GetPages())
            {
                sb.AppendLine(page.Text);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        // ---------------- PDF -> PNG (Scanned) ----------------
        private List<string> RenderPdfToPngs(string pdfPath, int dpi)
        {
            // Requires Ghostscript installed + in PATH
            var outDir = Path.Combine(Path.GetTempPath(), "ocr_pdf_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outDir);

            var outFiles = new List<string>();

            var settings = new MagickReadSettings
            {
                Density = new Density(dpi),
                FrameIndex = 0,
                FrameCount = 0
            };

            using var images = new MagickImageCollection();
            images.Read(pdfPath, settings);

            int i = 0;
            foreach (var img in images)
            {
                img.Alpha(AlphaOption.Remove);
                img.ColorSpace = ColorSpace.sRGB;
                img.Format = MagickFormat.Png;

                var outPath = Path.Combine(outDir, $"page_{++i}.png");
                img.Write(outPath);
                outFiles.Add(outPath);
            }

            return outFiles;
        }

        // ---------------- TESSERACT ----------------
        private OcrTextResult RunTesseractOnImage(string imagePath, string lang)
            => RunTesseractOnImages(new List<string> { imagePath }, lang);

        private OcrTextResult RunTesseractOnImages(List<string> imagePaths, string lang)
        {
            var tessdata = Path.Combine(_env.ContentRootPath, _cfg["Ocr:TessdataRelativePath"] ?? "OCR\\tessdata");

            if (!Directory.Exists(tessdata))
                throw new DirectoryNotFoundException($"tessdata not found: {tessdata}");

            // must contain eng.traineddata etc
            var sb = new StringBuilder();
            float totalConf = 0;
            int totalWords = 0;
            int pageCount = 0;

            using var engine = new TesseractEngine(tessdata, lang, EngineMode.Default);

            foreach (var p in imagePaths)
            {
                using var img = Pix.LoadFromFile(p);
                using var page = engine.Process(img);
                var text = page.GetText() ?? "";
                var conf = page.GetMeanConfidence();

                sb.AppendLine(text);
                sb.AppendLine("\n--- PAGE BREAK ---\n");

                totalConf += conf;
                totalWords += CountWords(text);
                pageCount++;
            }

            return new OcrTextResult
            {
                Text = sb.ToString(),
                MeanConfidence = pageCount == 0 ? 0 : totalConf / pageCount,
                WordCount = totalWords
            };
        }

        // ---------------- PARSER (basic) ----------------
        private OcrParsedDto ParseInvoice(string text)
        {
            var p = new OcrParsedDto();
            if (string.IsNullOrWhiteSpace(text)) return p;

            // Supplier name (take first meaningful line)
            var lines = text.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 3).ToList();
            p.SupplierName = lines.FirstOrDefault();

            p.InvoiceNo = FindByRegex(text, @"Invoice\s*No\s*[:\-]?\s*([A-Za-z0-9\-\/]+)")
                       ?? FindByRegex(text, @"\bINV[-\/]?\d{2,}\b");

            var dateRaw = FindByRegex(text, @"Invoice\s*Date\s*[:\-]?\s*(\d{1,2}[\/\-]\d{1,2}[\/\-]\d{2,4})");
            p.InvoiceDate = ToIsoDate(dateRaw);

            p.SubTotal = FindMoney(text, @"Sub\s*Total\s*[:\-]?\s*([0-9,]+\.\d{2})");
            p.Discount = FindMoney(text, @"Discount\s*[:\-]?\s*([0-9,]+\.\d{2})");
            p.TaxAmount = FindMoney(text, @"Tax.*?\s*[:\-]?\s*([0-9,]+\.\d{2})");
            p.Total = FindMoney(text, @"Grand\s*Total\s*[:\-]?\s*([0-9,]+\.\d{2})")
                   ?? FindMoney(text, @"TOTAL\s*[:\-]?\s*([0-9,]+\.\d{2})");

            var pct = FindByRegex(text, @"GST\s*(\d{1,2})\%") ?? FindByRegex(text, @"Tax.*?(\d{1,2})\%");
            if (decimal.TryParse(pct, out var dp)) p.TaxPercent = dp;

            return p;
        }

        private static string? FindByRegex(string text, string pattern)
        {
            var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (!m.Success) return null;
            return m.Groups.Count > 1 ? m.Groups[1].Value.Trim() : m.Value.Trim();
        }

        private static decimal? FindMoney(string text, string pattern)
        {
            var s = FindByRegex(text, pattern);
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Replace(",", "").Trim();
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
            return null;
        }

        private static string? ToIsoDate(string? d)
        {
            if (string.IsNullOrWhiteSpace(d)) return null;
            d = d.Trim();

            // dd/MM/yyyy
            if (DateTime.TryParseExact(d, new[] { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt.ToString("yyyy-MM-dd");

            return null;
        }

        private static int CountWords(string s)
            => Regex.Matches(s ?? "", @"\b\w+\b").Count;

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
