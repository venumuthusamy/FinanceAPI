using FinanceApi.InterfaceService;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.ModelDTO.TB;

namespace FinanceApi.Services
{
    public class FinanceReportService : IFinanceReportService
    {
        private readonly IFinanceReportRepository _repo;

        public FinanceReportService(IFinanceReportRepository repo)
        {
            _repo = repo;
        }

        public Task<IEnumerable<TrialBalanceDTO>> GetTrialBalanceAsync(ReportBaseDTO dto)
        {
            return _repo.GetTrialBalanceAsync(dto);
        }

        public Task<IEnumerable<TrialBalanceDetailDTO>> GetTrialBalanceDetailAsync(
          TrialBalanceDetailRequestDTO dto)
        {
            return _repo.GetTrialBalanceDetailAsync(dto);
        }

        public Task<IEnumerable<ProfitLossViewInfo>> GetProfitLossDetails()
        {
            return _repo.GetProfitLossDetails();
        }

        public Task<IEnumerable<BalanceSheetViewInfo>> GetBalanceSheetAsync()
        {
            return _repo.GetBalanceSheetAsync();
        }
    }
}
