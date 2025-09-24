using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IPaymentTermsRepository
    {
        Task<IEnumerable<PaymentTermsDTO>> GetAllAsync();
        Task<PaymentTermsDTO?> GetByIdAsync(int id);
        Task<PaymentTerms> AddAsync(PaymentTerms paymentTerms);
        Task<bool> UpdateAsync(PaymentTerms paymentTerms);
        Task<bool> DeleteAsync(int id);
    }
}
