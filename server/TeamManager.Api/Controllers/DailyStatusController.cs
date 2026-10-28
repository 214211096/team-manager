using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;
using TeamManager.Api.Services;

namespace TeamManager.Api.Controllers;

/// <summary>ביצוע יומי מול יעד לפי יום (תאריך בפורמט yyyy-MM-dd).</summary>
[ApiController]
[Route("api/daily-status")]
[Authorize]
public class DailyStatusController(DailyStatusService dailyStatus, TimeProvider clock) : ControllerBase
{
    [HttpGet("{date}")]
    public Task<DailyStatusDayDto> Get(DateOnly date) => dailyStatus.GetDayAsync(date);

    [HttpPut("{date}")]
    [Authorize(Roles = nameof(UserRole.TeamLead))]
    public async Task<ActionResult<DailyStatusDayDto>> Save(DateOnly date, DailyStatusSaveRequest req)
    {
        if (date > DateOnly.FromDateTime(clock.GetLocalNow().DateTime))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "אי אפשר להזין ביצוע ליום עתידי");

        var day = await dailyStatus.GetDayAsync(date);
        if (day.Locked)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "החודש נעול אחרי שליחה לשכר, אי אפשר לשנות ביצוע");

        var error = await dailyStatus.SaveAsync(date, req.Entries);
        if (error is not null) return Problem(statusCode: error.StatusCode, title: error.Message);

        return await dailyStatus.GetDayAsync(date);
    }
}
