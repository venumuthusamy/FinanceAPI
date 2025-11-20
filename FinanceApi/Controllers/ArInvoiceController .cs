using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace UnityWorksERP.Finance.AR
{
    [ApiController]
    [Route("api/[controller]")]
    public class ArInvoiceController : ControllerBase
    {
        private readonly IArInvoiceService _service;

        public ArInvoiceController(IArInvoiceService service)
        {
            _service = service;
        }

        // GET api/ArInvoice/list
        [HttpGet("list")]
        public async Task<IActionResult> GetList()
        {
            var data = await _service.GetAllAsync();
            return Ok(new { data });
        }
    }
}
