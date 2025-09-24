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

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _service.GetAllAsync());
        }


        [HttpGet("get/{id}")]
        public async Task<IActionResult> GetById(long id)
        {
            var licenseObj = await _service.GetById(id);
            var data = new ResponseResult<object>(true, "Success", licenseObj);
            return Ok(data);
        }

        [HttpPost("insert")]
        public async Task<ActionResult> Create(FlagIssuesDTO flagIssuesDTO)
        {
            var id = await _service.CreateAsync(flagIssuesDTO);
            var data = new ResponseResult<object>(true, "FlagIssues Created Successfully", id);
            return Ok(data);

        }


        [HttpPut("update/{id}")]
        public async Task<IActionResult> Update(FlagIssuesDTO flagIssuesDTO)
        {
            await _service.UpdateAsync(flagIssuesDTO);
            var data = new ResponseResult<object>(true, "FlagIssues updated successfully.", null);
            return Ok(data);
        }



        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            var data = new ResponseResult<object>(true, "FlagIssues Deleted sucessfully", null);
            return Ok(data);
        }
    }
}
