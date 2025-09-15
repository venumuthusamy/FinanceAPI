using FinanceApi.Interfaces;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class ServicesService : IServicesService
    {
        private readonly IServicesRepository _repository;

        public ServicesService(IServicesRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<Service>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<Service?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<Service> CreateAsync(Service service)
        {

            return await _repository.CreateAsync(service);
        }

        public async Task<Service?> UpdateAsync(int id, Service service)
        {
            return await _repository.UpdateAsync(id, service);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }
    }
}
