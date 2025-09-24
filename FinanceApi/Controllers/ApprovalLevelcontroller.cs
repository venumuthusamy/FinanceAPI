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
    public class ApprovalLevelcontroller : ControllerBase
    {
        private readonly IApprovallevelSevice _service;

        public ApprovalLevelcontroller(IApprovallevelSevice service)
        {
            _service = service;
        }
        [HttpGet("GetApprovalLevels")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(new ResponseResult<IEnumerable<ApprovalLevelDTO>>(true, "Approval levels retrieved successfully", result));
        }

        // GET: api/ApprovalLevel/5
        [HttpGet("GetApprovalLevelById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var approvalLevel = await _service.GetByIdAsync(id);
            if (approvalLevel == null)
                return NotFound(new ResponseResult<ApprovalLevelDTO>(false, "Approval level not found", null));

            return Ok(new ResponseResult<ApprovalLevelDTO>(true, "Approval level retrieved successfully", approvalLevel));
        }

        // POST: api/ApprovalLevel
        [HttpPost("CreateApprovalLevel")]
        public async Task<IActionResult> Create([FromBody] ApprovalLevel approvalLevel)
        {
            if (approvalLevel == null)
                return BadRequest(new ResponseResult<ApprovalLevel>(false, "Invalid data", null));

            approvalLevel.CreatedDate = DateTime.Now;
            var created = await _service.CreateAsync(approvalLevel);
            return Ok(new ResponseResult<ApprovalLevel>(true, "Approval level created successfully", created));
        }

        // PUT: api/ApprovalLevel/5
        [HttpPut("UpdateApprovalLevelById/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ApprovalLevel approvalLevel)
        {
            if (approvalLevel == null)
                return BadRequest(new ResponseResult<ApprovalLevel>(false, "Invalid data", null));

            var updated = await _service.UpdateAsync(id, approvalLevel);
            if (!updated)
                return NotFound(new ResponseResult<ApprovalLevel>(false, "Approval level not found", null));

            return Ok(new ResponseResult<ApprovalLevel>(true, "Approval level updated successfully", approvalLevel));
        }

        // DELETE: api/ApprovalLevel/5
        [HttpDelete("DeleteApprovalLevelById/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound(new ResponseResult<string>(false, "Approval level not found", null));

            return Ok(new ResponseResult<string>(true, "Approval level deleted successfully", null));
        }
    }
}
