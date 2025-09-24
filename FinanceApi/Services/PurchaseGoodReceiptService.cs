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

        public async Task<int> CreateAsync(PurchaseGoodReceiptItemsDTO goodReceiptItemsDTO)
        {
            return await _repository.CreateAsync(goodReceiptItemsDTO);

        }

        public async Task<PurchaseGoodReceiptItemsDTO> GetById(long id)
        {
            return await _repository.GetByIdAsync(id);
        }
    }
}
