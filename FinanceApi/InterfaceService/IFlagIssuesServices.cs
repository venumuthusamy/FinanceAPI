using FinanceApi.ModelDTO;

namespace FinanceApi.InterfaceService
{
    public interface IflagIssuesServices
    {
        Task<IEnumerable<FlagIssuesDTO>> GetAllAsync();
        Task<int> CreateAsync(FlagIssuesDTO flagIssuesDTO);
        Task<FlagIssuesDTO> GetById(long id);
       Task UpdateAsync(FlagIssuesDTO flagIssuesDTO);

        Task DeleteAsync(int id);
    }
}
