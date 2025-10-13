using FinanceApi.ModelDTO;

namespace FinanceApi.Interfaces
{
    public interface ICatagoryRepository
    {
        Task<IEnumerable<CatagoryDTO>> GetAllAsync();
        Task<CatagoryDTO> GetByIdAsync(long id);
        Task<int> CreateAsync(CatagoryDTO catagoryDTO);
        Task UpdateAsync(CatagoryDTO catagoryDTO);
        Task DeactivateAsync(int id);
    }
}
