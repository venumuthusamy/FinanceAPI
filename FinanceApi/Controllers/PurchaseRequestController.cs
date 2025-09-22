using Azure.Core;
using FinanceApi.Data;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PurchaseRequestController : ControllerBase
    {
        private readonly IPurchaseRequestService _service;

        public PurchaseRequestController(IPurchaseRequestService service)
        {
            _service = service;
        }

        // GET: api/PurchaseRequest
        //[HttpGet]
        //[Route("GetPurchaseRequest")]
        //public async Task<IActionResult> GetAll()
        //{
        //    var prs = await _service.GetAllAsync();
        //    return Ok(new ResponseResult<IEnumerable<PurchaseRequestDTO>>(true, "Purchase requests retrieved successfully", prs));
        //}

        [HttpGet]
        [Route("GetPurchaseRequest")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(new ResponseResult<IEnumerable<PurchaseRequestDTO>>(true, "Purchase requests retrieved successfully", result));
        }
        // GET: api/PurchaseRequest/5
        [HttpGet("GetPurchaseRequestById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var pr = await _service.GetByIdAsync(id);
            if (pr == null)
                return NotFound(new ResponseResult<PurchaseRequest>(false, "Purchase request not found", null));

            return Ok(new ResponseResult<PurchaseRequest>(true, "Purchase request retrieved successfully", pr));
        }

        // POST: api/PurchaseRequest
        [HttpPost("CreatePurchaseRequest")]
       
        public async Task<IActionResult> Create([FromBody] PurchaseRequest pr)
        {
            if (pr == null)
                return BadRequest(new ResponseResult<PurchaseRequest>(false, "Invalid data", null));
            pr.CreatedDate = DateTime.Now;
            var created = await _service.CreateAsync(pr);
            return Ok(new ResponseResult<PurchaseRequest>(true, "Purchase request created successfully", created));
        }

        // PUT: api/PurchaseRequest/5
        [HttpPut("UpdatePurchaseRequestById/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] PurchaseRequest pr)
        {
            if (pr == null)
                return BadRequest(new ResponseResult<PurchaseRequest>(false, "Invalid data", null));

            var updated = await _service.UpdateAsync(id, pr);
            if (!updated)
                return NotFound(new ResponseResult<PurchaseRequest>(false, "Purchase request not found", null));

            return Ok(new ResponseResult<PurchaseRequest>(true, "Purchase request updated successfully", pr));
        }

        // DELETE: api/PurchaseRequest/5
        [HttpDelete("DeletePurchaseRequestById/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound(new ResponseResult<PurchaseRequest>(false, "Purchase request not found", null));

            return Ok(new ResponseResult<string>(true, "Purchase request deleted successfully", null));
        }
    }
}
