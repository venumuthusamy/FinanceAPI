using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IStateRepository
    {
        Task<IEnumerable<StateDto>> GetAllAsync();
        Task<StateDto> GetByIdAsync(long id);
        Task<int> CreateAsync(State state);
        Task UpdateAsync(State state);
        Task DeactivateAsync(int id);
    }
}
