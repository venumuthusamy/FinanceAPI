using FinanceApi.ModelDTO;

namespace FinanceApi.InterfaceService
{
    public interface IIncotermsService
    {
        Task<IncotermsDTO> GetById(long id);
        Task<int> CreateAsync(IncotermsDTO incotermsDTO);
        Task<IEnumerable<IncotermsDTO>> GetAllAsync();

        Task DeleteLicense(int id);

        Task UpdateLicense(IncotermsDTO incotermsDTO);
    }
}
