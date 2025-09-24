using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class SuppliersService : ISuppliersService
    {
        private readonly ISuppliersRepository _repository;

        public SuppliersService(ISuppliersRepository repository)
        {
            _repository = repository;
        }

        // Get all suppliers
        public Task<IEnumerable<SuppliersDTO>> GetAllAsync()
        {
            return _repository.GetAllAsync();
        }

        // Get supplier by id
        public Task<SuppliersDTO?> GetByIdAsync(int id)
        {
            return _repository.GetByIdAsync(id);
        }

        // Create a new supplier
        public Task<Suppliers> CreateAsync(Suppliers supplier)
        {
            return _repository.AddAsync(supplier);
        }

        // Update supplier
        public Task<bool> UpdateAsync(int id, Suppliers supplier)
        {
            supplier.Id = id;
            return _repository.UpdateAsync(supplier);
        }

        // Delete supplier (soft delete)
        public Task<bool> DeleteAsync(int id)
        {
            return _repository.DeleteAsync(id);
        }
    }

}

