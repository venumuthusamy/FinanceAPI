// Services/PickingService.cs
using System.Globalization;
using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class PickingService : IPickingService
    {
        private readonly IPickingRepository _repository;
        private readonly IRunningNumberRepository _seq;
        private readonly ICodeImageService _img;

        public PickingService(IPickingRepository repository, IRunningNumberRepository seq,
        ICodeImageService img)
        {
            _repository = repository;
            _seq = seq;
            _img = img;
        }

        public Task<IEnumerable<PickingDTO>> GetAllAsync() => _repository.GetAllAsync();

        public Task<PickingDTO?> GetByIdAsync(int id) => _repository.GetByIdAsync(id);

        public Task<int> CreateAsync(Picking picking)
        {
            if (picking.LineItems is null || picking.LineItems.Count == 0)
                throw new ArgumentException("At least one line is required.");
            return _repository.CreateAsync(picking);
        }

        public Task UpdateAsync(Picking picking) => _repository.UpdateAsync(picking);

        public Task DeactivateAsync(int id, int updatedBy) => _repository.DeactivateAsync(id, updatedBy);

        // Simple rule; change to your business logic any time
        public (string barCode, string qrCode) GenerateCodes(int soId, DateTime? soDateUtc = null)
        {
            var bar = $"PK-{soId:000000}";
            var qr = $"PK|{soId}|{(soDateUtc ?? DateTime.UtcNow):yyyyMMdd}";
            return (bar, qr);
        }

        public async Task<CodesResponseEx> GenerateCodesAsync(CodesRequest req)
        {
            var dt = (req.SoDateUtc ?? DateTime.UtcNow).Date;

            // date parts
            var yy = dt.ToString("yy", CultureInfo.InvariantCulture);   // 25
            var mmdd = dt.ToString("MMdd", CultureInfo.InvariantCulture); // 1108
            var dateKey = dt.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            // daily running serial with zero pad to 4
            var serial = await _seq.NextPickingSerialAsync(dateKey);
            var serial4 = serial.ToString("0000", CultureInfo.InvariantCulture);

            // assemble barcode text
            var countryYear = $"{req.Country}{yy}";                         // SG25
            var barText = $"{req.Prefix}-{countryYear}-{mmdd}-{serial4}-{req.SoId}";   // PKL-SG25-1108-0012

            // QR human-readable text (two lines)
            var qrText = $"Packing List No: {barText}\nDate: {dt:dd-MMM-yyyy}";

            // images -> base64 data urls
            var barPng = _img.MakeBarcodePng(barText, width: 520, height: 140);
            var qrPng = _img.MakeQrPng(qrText, pixelsPerModule: 8);

            string ToDataUrl(byte[] bytes) => $"data:image/png;base64,{Convert.ToBase64String(bytes)}";

            return new CodesResponseEx(
                BarCode: barText,
                QrText: qrText,
                BarCodeSrcBase64: ToDataUrl(barPng),
                QrCodeSrcBase64: ToDataUrl(qrPng)
            );
        }
    }
}

