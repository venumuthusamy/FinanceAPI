using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using FinanceApi.Repositories;

namespace FinanceApi.Services
{
    public class PurchaseOrderService : IPurchaseOrderService
    {
        private readonly IPurchaseOrderRepository _repository;
        private readonly IConfiguration _config;
        private readonly ICodeImageService _img;

        public PurchaseOrderService(IPurchaseOrderRepository repository, IConfiguration config, ICodeImageService img)
        {
            _repository = repository;
            _config = config;
            _img = img;
        }

        public async Task<IEnumerable<PurchaseOrderDto>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<IEnumerable<PurchaseOrderDto>> GetAllDetailswithGRN()
        {
            return await _repository.GetAllDetailswithGRN();
        }

        public async Task<PurchaseOrderDto?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<int> CreateAsync(PurchaseOrder purchaseOrder)
        {
            // Example validation logic you might add later
            if (purchaseOrder.PoLines == null || !purchaseOrder.PoLines.Any())
                throw new ArgumentException("At least one line item is required.");

            return await _repository.CreateAsync(purchaseOrder);
        }

        public async Task UpdateAsync(PurchaseOrder purchaseOrder)
        {
            await _repository.UpdateAsync(purchaseOrder);
        }

        public async Task DeleteLicense(int id)
        {
            await _repository.DeactivateAsync(id);
        }
        public PoQrResponse BuildPoQr(string poNo)
        {
            // Put your real UI base URL here (NOT Angular :4200 in production)
            var baseUrl = _config["PublicUiBaseUrl"] ?? "http://192.168.6.148:4200";

            var payloadUrl = $"{baseUrl}/purchase/mobilereceiving?poNo={Uri.EscapeDataString(poNo)}";

            var qrPng = _img.MakeQrPng(payloadUrl, pixelsPerModule: 8);

            string ToDataUrl(byte[] bytes) => $"data:image/png;base64,{Convert.ToBase64String(bytes)}";

            return new PoQrResponse(
                PurchaseOrderNo: poNo,
                QrPayloadUrl: payloadUrl,
                QrCodeSrcBase64: ToDataUrl(qrPng)
            );
        }
    }
}
