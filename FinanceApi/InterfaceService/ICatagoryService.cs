using FinanceApi.ModelDTO;

namespace FinanceApi.InterfaceService
{
    public interface ICatagoryService
    {
        Task<IEnumerable<CatagoryDTO>> GetAllAsync();
        Task<int> CreateAsync(CatagoryDTO catagoryDTO);
        Task<CatagoryDTO> GetById(long id);
        Task Update(CatagoryDTO catagoryDTO);
        Task Delete(int id);
    }
}
