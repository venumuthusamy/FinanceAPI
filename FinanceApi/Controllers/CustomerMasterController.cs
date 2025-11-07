using FinanceApi.Data;
using FinanceApi.InterfaceService;
using FinanceApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerMasterController : ControllerBase
    {
        private readonly ICustomerMasterService _customerMasterService;


        public CustomerMasterController(ICustomerMasterService customerMasterService)
        {
            _customerMasterService = customerMasterService;
        }


        [HttpGet("GetAllCustomerMaster")]
        public async Task<IActionResult> GetAllCustomerMaster()
        {
            var list = await _customerMasterService.GetAllAsync();
            ResponseResult data = new ResponseResult(true, "Customer Master retrieved successfully", list);
            return Ok(data);
        }


        [HttpGet("GetCustomerMasterById/{id}")]
        public async Task<IActionResult> GetCustomerMasterById(int id)
        {
            var approvalLevel = await _customerMasterService.GetById(id);
            if (approvalLevel == null)
            {
                ResponseResult data1 = new ResponseResult(false, "Customer Master not found", null);
                return Ok(data1);
            }
            ResponseResult data = new ResponseResult(true, "Success", approvalLevel);
            return Ok(data);
        }




        [HttpPost("CreateCustomerMaster")]
        public async Task<ActionResult> CreateCustomerMaster(CustomerMaster customerMaster)
        {

            customerMaster.CreatedDate = DateTime.Now;
                var id = await _customerMasterService.CreateAsync(customerMaster);
                ResponseResult data = new ResponseResult(true, "Customer Master created successfully", id);
                return Ok(data);
           

        }

        [HttpPut("UpdateCustomerMasterById/{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CustomerMaster customerMaster)
        {
       

            await _customerMasterService.UpdateAsync(customerMaster);
            var ok = new ResponseResult(true, "Customer Master updated successfully.", null);
            return Ok(ok);
        }


        [HttpDelete("DeleteCustomerMasterById/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            await _customerMasterService.DeleteAsync(id);
            ResponseResult data = new ResponseResult(true, "Customer Master Deleted sucessfully", null);
            return Ok(data);
        }
    }
}
