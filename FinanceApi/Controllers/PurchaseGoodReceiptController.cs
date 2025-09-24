using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PurchaseGoodReceiptController : ControllerBase
    {
        private readonly IPurchaseGoodReceiptService _service;

        public PurchaseGoodReceiptController(IPurchaseGoodReceiptService service)
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
        public async Task<ActionResult> Create(PurchaseGoodReceiptItemsDTO goodReceiptItemsDTO)
        {
            var id = await _service.CreateAsync(goodReceiptItemsDTO);
             var data = new ResponseResult<object>(true, "PurchaseGoodItems Created Successfully", id);
            return Ok(data);

        }


    }
}
