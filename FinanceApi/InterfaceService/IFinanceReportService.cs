using FinanceApi.ModelDTO;
using FinanceApi.ModelDTO.TB;

namespace FinanceApi.InterfaceService
{
    public interface IFinanceReportService
    {
        Task<IEnumerable<TrialBalanceDTO>> GetTrialBalanceAsync(ReportBaseDTO dto);

        Task<IEnumerable<TrialBalanceDetailDTO>> GetTrialBalanceDetailAsync(
           TrialBalanceDetailRequestDTO dto);

        Task<IEnumerable<ProfitLossViewInfo>> GetProfitLossDetails();

        Task<IEnumerable<BalanceSheetViewInfo>> GetBalanceSheetAsync();
    }
}
