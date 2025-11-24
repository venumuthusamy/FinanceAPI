using FinanceApi.Data;
using FinanceApi.InterfaceService;
using FinanceApi.ModelDTO;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

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
            return Ok(new { success = true, data = list });
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
            if (dto.AccountId == null)
                return BadRequest(new { success = false, message = "Account is required." });

            if (string.IsNullOrWhiteSpace(dto.JournalDate))
                return BadRequest(new { success = false, message = "Journal Date is required." });

            if (dto.Debit <= 0 && dto.Credit <= 0)
                return BadRequest(new { success = false, message = "Enter either debit or credit amount." });

            // Timezone from client, default if missing
            if (string.IsNullOrWhiteSpace(dto.Timezone))
                dto.Timezone = "Asia/Kolkata";    // or "UTC"

            var tz = TimeZoneInfo.FindSystemTimeZoneById(dto.Timezone);

            // JournalDate (local→UTC)
            var localJournalDate = DateTime.SpecifyKind(
                DateTime.Parse(dto.JournalDate, CultureInfo.InvariantCulture),
                DateTimeKind.Unspecified);

            dto.JournalDateUtc = TimeZoneInfo.ConvertTimeToUtc(localJournalDate, tz);

            // RecurringStartDate
            if (!string.IsNullOrEmpty(dto.RecurringStartDate))
            {
                var localStart = DateTime.SpecifyKind(
                    DateTime.Parse(dto.RecurringStartDate, CultureInfo.InvariantCulture),
                    DateTimeKind.Unspecified);

                dto.RecurringStartDateUtc =
                    TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
            }

            // RecurringEndDate
            if (!string.IsNullOrEmpty(dto.RecurringEndDate))
            {
                var localEnd = DateTime.SpecifyKind(
                    DateTime.Parse(dto.RecurringEndDate, CultureInfo.InvariantCulture),
                    DateTimeKind.Unspecified);

                dto.RecurringEndDateUtc =
                    TimeZoneInfo.ConvertTimeToUtc(localEnd, tz);
            }

            var id = await _journalService.CreateAsync(dto);
            var data = await _journalService.GetById(id);

            return Ok(new { success = true, id, data });
        }

        [HttpPost("process-recurring")]
        public async Task<IActionResult> ProcessRecurring([FromQuery] string? timezone = null)
        {
            timezone ??= "Asia/Kolkata";

            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

            var processed = await _journalService.ProcessRecurringAsync(nowLocal, timezone);

            return Ok(new { success = true, processed, runAt = nowLocal, timezone });
        }


        [HttpPost("post-batch")]
        public async Task<IActionResult> PostBatch([FromBody] JournalPostRequest request)
        {
            if (request == null || request.Ids == null || request.Ids.Count == 0)
            {
                return BadRequest("No journal ids provided.");
            }

            var updated = await _journalService.MarkAsPostedAsync(request.Ids);

            var result = new ResponseResult(true, "Posted successfully", updated);
            return Ok(result);
        }
    }
}
