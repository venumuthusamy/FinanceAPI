using FinanceApi.Interfaces;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class UomService : IUomService
    {
        private readonly IUomRepository _repository;

        public UomService(IUomRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<Uom>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<Uom?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<Uom> CreateAsync(Uom uom)
        {

            return await _repository.CreateAsync(uom);
        }

        public async Task<Uom?> UpdateAsync(int id, Uom uom)
        {
            return await _repository.UpdateAsync(id, uom);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }
    }
}
