using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnityWorksERP.Finance.AR
{
    public class ArInvoiceService : IArInvoiceService
    {
        private readonly IArInvoiceRepository _repo;

        public ArInvoiceService(IArInvoiceRepository repo)
        {
            _repo = repo;
        }

        public Task<IEnumerable<ArInvoiceListDto>> GetAllAsync()
            => _repo.GetAllAsync();
    }
}
