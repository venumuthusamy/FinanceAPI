using FinanceApi.ModelDTO;

namespace FinanceApi.Interfaces
{
    public interface IFlagIssuesRepository
    {
        Task<IEnumerable<FlagIssuesDTO>> GetAllAsync();
        Task<FlagIssuesDTO> GetByIdAsync(long id);

        Task<int> CreateAsync(FlagIssuesDTO flagIssuesDTO);

        Task UpdateAsync(FlagIssuesDTO flagIssuesDTO);

        Task DeactivateAsync(int id);
    }
}
