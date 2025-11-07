using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class CustomerMasterService : ICustomerMasterService
    {
        private readonly ICustomerMasterRepository _customerMasterRepository;

        public CustomerMasterService(ICustomerMasterRepository customerMasterRepository)
        {
            _customerMasterRepository = customerMasterRepository;
        }

        public async Task<IEnumerable<CustomerMasterDTO>> GetAllAsync()
        {
            return await _customerMasterRepository.GetAllAsync();
        }

        public async Task<int> CreateAsync(CustomerMaster customerMaster)
        {
            return await _customerMasterRepository.CreateAsync(customerMaster);

        }

        public async Task<CustomerMasterDTO> GetById(int id)
        {
            return await _customerMasterRepository.GetByIdAsync(id);
        }

        public Task UpdateAsync(CustomerMaster customerMaster)
        {
            return _customerMasterRepository.UpdateAsync(customerMaster);
        }


        public async Task DeleteAsync(int id)
        {
            await _customerMasterRepository.DeactivateAsync(id);
        }
    }
}
