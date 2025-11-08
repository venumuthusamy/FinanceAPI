using FinanceApi.Data;
using FinanceApi.InterfaceService;
using FinanceApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static FinanceApi.ModelDTO.DeliveryOrderDtos;

namespace FinanceApi.Controllers
{
    // Controllers/DeliveryOrderController.cs
    [ApiController]
    [Route("api/[controller]")]
    public class DeliveryOrderController : ControllerBase
    {
        private readonly IDeliveryOrderService _svc;
        public DeliveryOrderController(IDeliveryOrderService svc) => _svc = svc;

        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] DoCreateRequest req)
        {
            var userId = 1; // get from auth in real life
            var id = await _svc.CreateAsync(req, userId);
            return Ok(new ResponseResult(true, "Created", id));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var (hdr, lines) = await _svc.GetAsync(id);
            return Ok(new ResponseResult(hdr != null, hdr != null ? "OK" : "Not found", new { header = hdr, lines }));
        }

        [HttpPut("{id}/Header")]
        public async Task<IActionResult> UpdateHeader(int id, DoUpdateHeaderRequest req)
        {
            var userId = 1;
            await _svc.UpdateHeaderAsync(id, req, userId);
            return Ok(new ResponseResult(true, "Updated", null));
        }

        [HttpPost("AddLine")]
        public async Task<IActionResult> AddLine(DoAddLineRequest req)
        {
            var userId = 1;
            var lineId = await _svc.AddLineAsync(req, userId);
            return Ok(new ResponseResult(true, "Line added", lineId));
        }

        [HttpDelete("RemoveLine/{lineId}")]
        public async Task<IActionResult> RemoveLine(int lineId)
        {
            await _svc.RemoveLineAsync(lineId);
            return Ok(new ResponseResult(true, "Removed", null));
        }

        [HttpPost("{id}/Submit")] public Task<IActionResult> Submit(int id) => setStat(id, 1);
        [HttpPost("{id}/Approve")] public Task<IActionResult> Approve(int id) => setStat(id, 2);
        [HttpPost("{id}/Reject")] public Task<IActionResult> Reject(int id) => setStat(id, 3);

        [HttpPost("{id}/Post")]
        public async Task<IActionResult> Post(int id)
        {
            var userId = 1;
            await _svc.PostAsync(id, userId);
            return Ok(new ResponseResult(true, "Posted", null));
        }

        private async Task<IActionResult> setStat(int id, int status)
        {
            var userId = 1;
            await (_svc as DeliveryOrderService)!.SubmitAsync(id, userId);
            if (status == 2) await _svc.ApproveAsync(id, userId);
            if (status == 3) await _svc.RejectAsync(id, userId);
            if (status == 1) await _svc.SubmitAsync(id, userId);
            return Ok(new ResponseResult(true, "OK", null));
        }
    }

}
