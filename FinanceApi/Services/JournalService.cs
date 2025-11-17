using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Services
{
    public class JournalService :IJournalService
    {
        private readonly IJournalRepository _journalRepository;
        public JournalService(IJournalRepository journalRepository)
        {
            _journalRepository = journalRepository;
        }

        public async Task<IEnumerable<JournalsDTO>> GetAllAsync()
        {
            return await _journalRepository.GetAllAsync();
        }
        public async Task<IEnumerable<ManualJournalDto>> GetAllRecurringDetails()
        {
            return await _journalRepository.GetAllRecurringDetails();
        }



        public async Task<int> CreateAsync(ManualJournalCreateDto dto)
        {
            return await _journalRepository.CreateAsync(dto);

        }


        public async Task<ManualJournalDto> GetById(int id)
        {
            return await _journalRepository.GetByIdAsync(id);
        }


        public Task<int> ProcessRecurringAsync(DateTime processDate)
            => _journalRepository.ProcessRecurringAsync(processDate);
    }
}
