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
    public class JournalController : ControllerBase
    {
        private readonly IJournalService _journalService;
        public JournalController(IJournalService journalService)
        {
            _journalService = journalService;
        }

        [HttpGet("GetAllJournals")]
        public async Task<IActionResult> GetAll()
        {
            var list = await _journalService.GetAllAsync();
            ResponseResult data = new ResponseResult(true, "Success", list);
            return Ok(data);
        }


        [HttpGet("get-all")]
        public async Task<IActionResult> GetAllRecurringDetails()
        {
            var data = await _journalService.GetAllRecurringDetails();
            return Ok(new { success = true, data });
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] ManualJournalCreateDto dto)
        {
            if (dto.AccountId == null || dto.JournalDate == null)
            {
                return BadRequest(new { success = false, message = "Account and JournalDate are required." });
            }

            if (dto.Debit <= 0 && dto.Credit <= 0)
            {
                return BadRequest(new { success = false, message = "Enter either debit or credit amount." });
            }

            var id = await _journalService.CreateAsync(dto);

            // get record back to include JournalNo
            var data = await _journalService.GetById(id);

            return Ok(new { success = true, id, data });
        }


        [HttpPost("process-recurring")]
        public async Task<IActionResult> ProcessRecurring()
        {
            var indiaTz = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            var nowIndia = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, indiaTz);
            var today = nowIndia.Date;

            var processed = await _journalService.ProcessRecurringAsync(today);

            return Ok(new { success = true, processed, date = today });
        }

    }
}
