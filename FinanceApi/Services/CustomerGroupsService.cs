using FinanceApi.Interfaces;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class CustomerGroupsService : ICustomerGroupsService
    {
        private readonly ICustomerGroupsRepository _repository;

        public CustomerGroupsService(ICustomerGroupsRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<CustomerGroups>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<CustomerGroups?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<CustomerGroups> CreateAsync(CustomerGroups customerGroups)
        {

            return await _repository.CreateAsync(customerGroups);
        }

        public async Task<CustomerGroups?> UpdateAsync(int id, CustomerGroups customerGroups)
        {
            return await _repository.UpdateAsync(id, customerGroups);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }
    }
}
