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
            var list = await _service.GetAllAsync();
            ResponseResult data = new ResponseResult(true, "PurchaseGoodReceipt retrieved successfully", list);
            return Ok(data);
        }

        [HttpGet("get/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var approvalLevel = await _service.GetById(id);
            if (approvalLevel == null)
            {
                ResponseResult data1 = new ResponseResult(false, "PurchaseGoodReceipt not found", null);
                return Ok(data1);
            }
            ResponseResult data = new ResponseResult(true, "Success", approvalLevel);
            return Ok(data);
        }

        [HttpPost("insert")]
        public async Task<ActionResult> Create(PurchaseGoodReceiptItemsDTO purchaseGoodReceiptItems)
        {
            var id = await _service.CreateAsync(purchaseGoodReceiptItems);
            ResponseResult data = new ResponseResult(true, "PurchaseGoodReceipt created successfully", id);
            return Ok(data);

        }
    }
}
