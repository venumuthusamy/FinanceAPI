using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IStateService
    {
        Task<IEnumerable<StateDto>> GetAllAsync();
        Task<StateDto> GetById(int id);
        Task<int> CreateAsync(State state);
        Task UpdateAsync(State state);
        Task DeleteAsync(int id);
    }
}
