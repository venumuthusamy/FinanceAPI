using FinanceApi.ModelDTO;
using FinanceApi.ModelDTO.TB;

namespace FinanceApi.Interfaces
{
    public interface IFinanceReportRepository
    {
        Task<IEnumerable<TrialBalanceDTO>> GetTrialBalanceAsync(ReportBaseDTO dto);

    }
}
