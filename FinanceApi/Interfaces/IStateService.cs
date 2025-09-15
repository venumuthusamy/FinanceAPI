using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IStateService
    {
        Task<List<StateDto>> GetAllAsync();
        Task<StateDto?> GetByIdAsync(int id);
        Task<State> CreateAsync(State state);
        Task<State?> UpdateAsync(int id, State state);
        Task<bool> DeleteAsync(int id);
    }
}
