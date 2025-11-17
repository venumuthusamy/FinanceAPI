using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface IJournalRepository
    {
        Task<IEnumerable<JournalsDTO>> GetAllAsync();
        Task<IEnumerable<ManualJournalDto>> GetAllRecurringDetails();
        Task<int> CreateAsync(ManualJournalCreateDto dto);

        Task<ManualJournalDto?> GetByIdAsync(int id);

        Task<int> ProcessRecurringAsync(DateTime processDate);
    }
}
