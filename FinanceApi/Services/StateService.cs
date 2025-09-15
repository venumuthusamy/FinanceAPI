using FinanceApi.Interfaces;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class StateService : IStateService
    {
        private readonly IStateRepository _repository;

        public StateService(IStateRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<StateDto>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<StateDto?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<State> CreateAsync(State state)
        {

            return await _repository.CreateAsync(state);
        }

        public async Task<State?> UpdateAsync(int id, State state)
        {
            return await _repository.UpdateAsync(id, state);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _repository.DeleteAsync(id);
        }
    }
}
