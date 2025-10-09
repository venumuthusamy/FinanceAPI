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
    }
}
