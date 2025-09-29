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
    public class FlagIssuesController : ControllerBase
    {
        private readonly IflagIssuesServices _service;
        public FlagIssuesController(IflagIssuesServices service)
        {
            _service = service;
        }


        [HttpGet("GetAllFlagissue")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            ResponseResult data = new ResponseResult(true, "Success", list);
            return Ok(data);
        }



        [HttpPost("createFlagissue")]
        public async Task<ActionResult> Create(FlagIssues flagIssuesDTO)
        {

            var id = await _service.CreateAsync(flagIssuesDTO);
            ResponseResult data = new ResponseResult(true, "FlagIssues created sucessfully", id);
            return Ok(data);

        }


       
        [HttpGet("getFlagissueById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var licenseObj = await _service.GetById(id);
            ResponseResult data = new ResponseResult(true, "Success", licenseObj);
            return Ok(data);
        }


        [HttpPut("updateFlagissueById/{id}")]
        public async Task<IActionResult> Update(FlagIssues flagIssuesDTO)
        {
            await _service.UpdateAsync(flagIssuesDTO);
            ResponseResult data = new ResponseResult(true, "FlagIssues updated successfully.", null);
            return Ok(data);
        }



        [HttpDelete("deleteFlagissueById/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            ResponseResult data = new ResponseResult(true, "FlagIssues Deleted sucessfully", null);
            return Ok(data);
        }
    }
}
