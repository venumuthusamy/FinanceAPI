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


        [HttpGet("GetAllIncoterms")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            ResponseResult data = new ResponseResult(true, "Success", list);
            return Ok(data);
        }



        [HttpPost("createIncoterms")]
        public async Task<ActionResult> Create(IncotermsDTO incotermsDTO)
        {

            var id = await _service.CreateAsync(incotermsDTO);
            ResponseResult data = new ResponseResult(true, "Incoterms created sucessfully", id);
            return Ok(data);

        }


        [HttpGet("getIncotermsById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var licenseObj = await _service.GetById(id);
            ResponseResult data = new ResponseResult(true, "Success", licenseObj);
            return Ok(data);
        }

        [HttpPut("updateIncotermsById/{id}")]
        public async Task<IActionResult> Update(IncotermsDTO incotermsDTO)
        {
            await _service.UpdateLicense(incotermsDTO);
            ResponseResult data = new ResponseResult(true, "Incoterms updated successfully.", null);
            return Ok(data);
        }



        [HttpDelete("deleteIncotermsById/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteLicense(id);
            ResponseResult data = new ResponseResult(true, "Incoterms Deleted sucessfully", null);
            return Ok(data);
        }
    }
}
