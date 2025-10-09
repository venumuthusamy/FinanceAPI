﻿using Azure.Core;
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


        [HttpGet("GetPurchaseRequest")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _service.GetAllAsync();
            ResponseResult data = new ResponseResult(true, "Parchase Request retrieved successfully", list);
            return Ok(data);
        }

        [HttpGet("GetPurchaseRequestById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var approvalLevel = await _service.GetById(id);
            if (approvalLevel == null)
            {
                ResponseResult data1 = new ResponseResult(false, "Parchase Request not found", null);
                return Ok(data1);
            }
            ResponseResult data = new ResponseResult(true, "Success", approvalLevel);
            return Ok(data);
        }

        [HttpGet("GetAvailablePurchaseRequests")]
        public async Task<IActionResult> GetAvailablePurchaseRequests()
        {
            var result = await _service.GetAvailablePurchaseRequestsAsync();
            return Ok(new { isSuccess = true, message = "Available PRs retrieved", data = result });
        }


        [HttpPost("CreatePurchaseRequest")]
        public async Task<ActionResult> Create(PurchaseRequest pr)
        {
            pr.CreatedDate = DateTime.Now;
            var id = await _service.CreateAsync(pr);
            ResponseResult data = new ResponseResult(true, "Purchase Request created successfully", id);
            return Ok(data);

        }

        [HttpPut("UpdatePurchaseRequestById/{id}")]
        public async Task<IActionResult> Update(PurchaseRequest pr)
        {
            await _service.UpdateAsync(pr);
            ResponseResult data = new ResponseResult(true, "Purchase Request updated successfully.", null);
            return Ok(data);
        }

        [HttpDelete("DeletePurchaseRequestById/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteLicense(id);
            ResponseResult data = new ResponseResult(true, "Purchase Request Deleted sucessfully", null);
            return Ok(data);
        }
    }
}
