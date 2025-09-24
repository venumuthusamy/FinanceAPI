using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.InterfaceService
{
    public interface IPaymentTermsService
    {
        Task<IEnumerable<PaymentTermsDTO>> GetAllAsync();
        Task<PaymentTermsDTO> GetByIdAsync(int id);
        Task<PaymentTerms> CreateAsync(PaymentTerms paymentTerms);
        Task<bool> UpdateAsync(int id, PaymentTerms paymentTerms);
        Task<bool> DeleteAsync(int id);
    }
}
