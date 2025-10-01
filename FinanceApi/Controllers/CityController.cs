using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CityController : ControllerBase
    {
        private readonly ICityService _service;

        public CityController(ICityService service)
        {
            _service = service;
        }

        [HttpGet("GetAllCities")]
        public async Task<IActionResult> getAllCitites()
        {
            var list = await _service.GetAllAsync();
            ResponseResult data = new ResponseResult(true, "Success", list);
            return Ok(data);
        }


        [HttpGet("GetByIdCities/{id}")]
        public async Task<IActionResult> getbyIdCitites(int id)
        {
            var licenseObj = await _service.GetById(id);
            ResponseResult data = new ResponseResult(true, "Success", licenseObj);
            return Ok(data);
        }


        [HttpPost("CreateCities")]
        public async Task<ActionResult> CreateCities(City city)
        {

            var id = await _service.CreateAsync(city);
            ResponseResult data = new ResponseResult(true, "City created sucessfully", id);
            return Ok(data);

        }

        [HttpPut("updateCities")]
        public async Task<IActionResult> updateCities(City city)
        {
            await _service.UpdateAsync(city);
            ResponseResult data = new ResponseResult(true, "City updated successfully.", null);
            return Ok(data);
        }



        [HttpDelete("deleteCities/{id}")]
        public async Task<IActionResult> deleteCities(int id)
        {
            await _service.DeleteAsync(id);
            ResponseResult data = new ResponseResult(true, "City Deleted sucessfully", null);
            return Ok(data);
        }


        [HttpGet("GetStateWithCountryId/{id}")]
        public async Task<IActionResult> GetStateWithCountryId(int id)
        {
            var licenseObj = await _service.GetStateWithCountryId(id);
            ResponseResult data = new ResponseResult(true, "Success", licenseObj);
            return Ok(data);
        }


        [HttpGet("GetCityWithStateId/{id}")]
        public async Task<IActionResult> GetCityWithStateId(int id)
        {
            var licenseObj = await _service.GetCityWithStateId(id);
            ResponseResult data = new ResponseResult(true, "Success", licenseObj);
            return Ok(data);
        }

    }
}
