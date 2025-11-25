using FinanceApi.ModelDTO;
using FinanceApi.ModelDTO.TB;

namespace FinanceApi.InterfaceService
{
    public interface IFinanceReportService
    {
        Task<IEnumerable<TrialBalanceDTO>> GetTrialBalanceAsync(ReportBaseDTO dto);
       
    }
}
