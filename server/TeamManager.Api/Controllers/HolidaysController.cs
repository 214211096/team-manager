using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamManager.Api.Services;

namespace TeamManager.Api.Controllers;

/// <summary>חגים (לא ימי עבודה) בטווח תאריכים, לסימון בלוח השנה ובמסכי ההזנה.</summary>
[ApiController]
[Route("api/holidays")]
[Authorize]
public class HolidaysController : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<Holiday>> List([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (to < from || to.DayNumber - from.DayNumber > 400)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "טווח תאריכים לא תקין");
        return Ok(HolidayCalendar.Between(from, to));
    }
}
