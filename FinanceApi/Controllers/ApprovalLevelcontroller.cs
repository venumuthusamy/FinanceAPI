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
            var list = await _service.GetAllAsync();
            ResponseResult data = new ResponseResult(true, "Approval levels retrieved successfully", list);
            return Ok(data);
        }
   

        [HttpGet("GetApprovalLevelById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var approvalLevel = await _service.GetById(id);
            if (approvalLevel == null)
            {
                ResponseResult data1 = new ResponseResult(false, "Approval level not found", null);
                return Ok(data1);
            }
            ResponseResult data = new ResponseResult(true, "Success", approvalLevel);
            return Ok(data);
        }

     


        [HttpPost("CreateApprovalLevel")]
        public async Task<ActionResult> Create(ApprovalLevel approvalLevel)
        {
            approvalLevel.CreatedDate = DateTime.Now;
            var id = await _service.CreateAsync(approvalLevel);
            ResponseResult data = new ResponseResult(true, "Approval level created successfully", id);
            return Ok(data);

        }
       
        [HttpPut("UpdateApprovalLevelById/{id}")]
        public async Task<IActionResult> Update(ApprovalLevel approvalLevel)
        {
            await _service.UpdateAsync(approvalLevel);
            ResponseResult data = new ResponseResult(true, "Approval level updated successfully.", null);
            return Ok(data);
        }

        [HttpDelete("DeleteApprovalLevelById/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteLicense(id);
            ResponseResult data = new ResponseResult(true, "Approval level Deleted sucessfully", null);
            return Ok(data);
        }
    }
}
