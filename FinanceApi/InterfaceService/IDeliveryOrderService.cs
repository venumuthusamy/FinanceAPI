// File: InterfaceService/IDeliveryOrderService.cs
using static FinanceApi.ModelDTO.DeliveryOrderDtos;

namespace FinanceApi.InterfaceService
{
    public interface IDeliveryOrderService
    {
        Task<int> CreateAsync(DoCreateRequest req, int userId);
        Task<(DoHeaderDto? hdr, IEnumerable<DoLineDto> lines)> GetAsync(int id);
        Task<IEnumerable<DoHeaderDto>> GetAllAsync();

        Task UpdateHeaderAsync(int id, DoUpdateHeaderRequest req, int userId);
        Task<int> AddLineAsync(DoAddLineRequest req, int userId);
        Task RemoveLineAsync(int lineId);

        Task SubmitAsync(int id, int userId);
        Task ApproveAsync(int id, int userId);
        Task RejectAsync(int id, int userId);
        Task PostAsync(int id, int userId);
        Task<IEnumerable<object>> GetSoRedeliveryViewAsync(int doId, int soId);
        Task<IEnumerable<DoLineDto>> GetLinesAsync(int doId);
    }
}
