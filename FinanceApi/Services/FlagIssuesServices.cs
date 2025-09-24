using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;

namespace FinanceApi.Services
{
    public class FlagIssuesServices : IflagIssuesServices
    {
        private readonly IFlagIssuesRepository _repository;

        public FlagIssuesServices(IFlagIssuesRepository repository)
        {
            _repository = repository;
        }

        public async Task<IEnumerable<FlagIssuesDTO>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<int> CreateAsync(FlagIssuesDTO flagIssuesDTO)
        {
            return await _repository.CreateAsync(flagIssuesDTO);

        }

        public async Task<FlagIssuesDTO> GetById(long id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public Task UpdateAsync(FlagIssuesDTO flagIssuesDTO)
        {
            return _repository.UpdateAsync(flagIssuesDTO);
        }


        public async Task DeleteAsync(int id)
        {
            await _repository.DeactivateAsync(id);
        }

    }
}
