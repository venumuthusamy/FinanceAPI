using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using System.ComponentModel;

namespace FinanceApi.Services
{
    public class PurchaseGoodReceiptService : IPurchaseGoodReceiptService
    {
        private readonly IPurchaseGoodReceiptRepository _repository;

        public PurchaseGoodReceiptService(IPurchaseGoodReceiptRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<PurchaseGoodReceiptItemsDTO>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<int> CreateAsync(PurchaseGoodReceiptItems goodReceiptItemsDTO)
        {
            return await _repository.CreateAsync(goodReceiptItemsDTO);

        }

        public async Task<PurchaseGoodReceiptItemsDTO> GetById(long id)
        {
            return await _repository.GetByIdAsync(id);
        }


        public async Task<IEnumerable<PurchaseGoodReceiptItemsViewInfo>> GetAllGRNDetailsAsync()
        {
            return await _repository.GetAllDetailsAsync();
        }

        public Task UpdateAsync(PurchaseGoodReceiptItems purchaseGoodReceipt)
        {
            return _repository.UpdateAsync(purchaseGoodReceipt);
        }


        public async Task DeleteAsync(int id)
        {
            await _repository.DeactivateAsync(id);
        }
        public async Task<IEnumerable<PurchaseGoodReceiptItemsDTO>> GetAllGRNByPoId()
        {
            return await _repository.GetAllGRNByPoId();
        }

        public async Task ApplyGrnAndUpdateSalesOrderAsync(ApplyGrnAndSalesOrderRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request), "Request cannot be null.");

            if (string.IsNullOrWhiteSpace(request.ItemCode))
                throw new ArgumentException("ItemCode cannot be empty.");

            await _repository.ApplyGrnAndUpdateSalesOrderAsync(
                request.ItemCode,
                request.WarehouseId,
                request.SupplierId,
                request.BinId,
                request.ReceivedQty
            );
        }
    }
}
