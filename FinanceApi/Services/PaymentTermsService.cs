using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class PaymentTermsService : IPaymentTermsService
    {
        private readonly IPaymentTermsRepository _repository;

        public PaymentTermsService(IPaymentTermsRepository repository)
        {
            _repository = repository;
        }


        public Task<PaymentTerms> CreateAsync(PaymentTerms paymentTerms)
        {
            return _repository.AddAsync(paymentTerms);
        }

        public Task<bool> DeleteAsync(int id)
        {
            return _repository.DeleteAsync(id);
        }


        public Task<IEnumerable<PaymentTermsDTO>> GetAllAsync()
        {
            return _repository.GetAllAsync();
        }

        public Task<bool> UpdateAsync(int id, PaymentTerms paymentTerms)
        {
            paymentTerms.Id = id;  // now 'id' exists
            return _repository.UpdateAsync(paymentTerms);
        }

        Task<PaymentTermsDTO> IPaymentTermsService.GetByIdAsync(int id)
        {
            return _repository.GetByIdAsync(id);
        }
    }
}
