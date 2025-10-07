﻿// Controllers/SupplierInvoiceController.cs
using FinanceApi.Data;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using FinanceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SupplierInvoicePinController : ControllerBase
    {
        private readonly ISupplierInvoicePinService _service;
        public SupplierInvoicePinController(ISupplierInvoicePinService service) => _service = service;

        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
            => Ok(new ResponseResult(true, "Retrieved", await _service.GetAllAsync()));

        [HttpGet("GetById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var data = await _service.GetByIdAsync(id);
            return Ok(new ResponseResult(data != null, data != null ? "Success" : "Not found", data));
        }

        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] SupplierInvoicePin dto)
        {
            var id = await _service.CreateAsync(dto);
            return Ok(new ResponseResult(true, "Supplier Invoice Created", id));
        }

        [HttpPut("Update/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] SupplierInvoicePin dto)
        {
            dto.Id = id;
            await _service.UpdateAsync(dto);
            return Ok(new ResponseResult(true, "Supplier Invoice Updated", null));
        }

        [HttpDelete("Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _service.DeleteAsync(id);
            return Ok(new ResponseResult(true, "Supplier Invoice Deleted", null));
        }
    }
}
