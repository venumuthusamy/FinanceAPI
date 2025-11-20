using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnityWorksERP.Finance.AR
{
    public interface IArInvoiceService
    {
        Task<IEnumerable<ArInvoiceListDto>> GetAllAsync();
    }
}
