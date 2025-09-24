using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class IncotermsController : ControllerBase
    {
        private readonly IIncotermsService _service;

        public IncotermsController (IIncotermsService service)
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
        public async Task<ActionResult> Create(IncotermsDTO incotermsDTO)
        {
            var id = await _service.CreateAsync(incotermsDTO);
            var data = new ResponseResult<object>(true, "Incoterms Created Successfully", id);
            return Ok(data);

        }


        [HttpPut("update/{id}")]
        public async Task<IActionResult> Update(IncotermsDTO incotermsDTO)
        {
            await _service.UpdateLicense(incotermsDTO);
            var data = new ResponseResult<object>(true, "Incoterms updated successfully.", null);
            return Ok(data);
        }



        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteLicense(id);
            var data = new ResponseResult<object>(true, "Incoterms Deleted sucessfully", null);
            return Ok(data);
        }
    }
}
