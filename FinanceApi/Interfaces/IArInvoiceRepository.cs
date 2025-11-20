using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnityWorksERP.Finance.AR
{
    public interface IArInvoiceRepository
    {
        Task<IEnumerable<ArInvoiceListDto>> GetAllAsync();
    }
}
