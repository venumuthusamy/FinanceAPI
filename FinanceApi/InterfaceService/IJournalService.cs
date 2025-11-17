using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.InterfaceService
{
    public interface IJournalService
    {
        Task<IEnumerable<JournalsDTO>> GetAllAsync();
        Task<int> CreateAsync(ManualJournalCreateDto dto);
        Task<IEnumerable<ManualJournalDto>> GetAllRecurringDetails();
        Task<ManualJournalDto> GetById(int id);

        Task<int> ProcessRecurringAsync(DateTime processDate);
    }
}
