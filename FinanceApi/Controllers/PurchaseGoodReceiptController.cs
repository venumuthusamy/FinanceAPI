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


        [HttpGet("GetAllGRN")]
        public async Task<IActionResult> GetAllGRN()
        {
            var list = await _service.GetAllAsync();
            ResponseResult data = new ResponseResult(true, "PurchaseGoodReceipt retrieved successfully", list);
            return Ok(data);
        }

        [HttpGet("getGRNbyId/{id}")]
        public async Task<IActionResult> getGRNbyId(int id)
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

        [HttpPost("insertGRN")]
        public async Task<ActionResult> insertGRN(PurchaseGoodReceiptItemsDTO purchaseGoodReceiptItems)
        {
            var id = await _service.CreateAsync(purchaseGoodReceiptItems);
            ResponseResult data = new ResponseResult(true, "PurchaseGoodReceipt created successfully", id);
            return Ok(data);

        }
    }
}
