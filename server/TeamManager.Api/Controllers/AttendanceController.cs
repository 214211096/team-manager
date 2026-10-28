using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;
using TeamManager.Api.Services;

namespace TeamManager.Api.Controllers;

/// <summary>נוכחות בוקר לפי יום (תאריך בפורמט yyyy-MM-dd).</summary>
[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController(AttendanceService attendance, TimeProvider clock) : ControllerBase
{
    [HttpGet("{date}")]
    public Task<AttendanceDayDto> Get(DateOnly date) => attendance.GetDayAsync(date);

    [HttpPut("{date}")]
    [Authorize(Roles = nameof(UserRole.TeamLead))]
    public async Task<ActionResult<AttendanceDayDto>> Save(DateOnly date, AttendanceSaveRequest req)
    {
        if (date > DateOnly.FromDateTime(clock.GetLocalNow().DateTime))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "אי אפשר לדווח נוכחות על יום עתידי");

        if (await attendance.IsLockedAsync(date))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "החודש נעול אחרי שליחה לשכר, אי אפשר לשנות נוכחות");

        var errors = AttendanceRules.Validate(req.Entries);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        var error = await attendance.SaveAsync(date, req.Entries);
        if (error is not null) return Problem(statusCode: error.StatusCode, title: error.Message);

        return await attendance.GetDayAsync(date);
    }

    /// <summary>שולח למנהלת את הנוכחות השמורה של היום. שליחה חוזרת יוצאת כ"עדכון".</summary>
    [HttpPost("{date}/send")]
    [Authorize(Roles = nameof(UserRole.TeamLead))]
    public async Task<ActionResult<AttendanceDayDto>> Send(DateOnly date)
    {
        var error = await attendance.SendReportAsync(date);
        if (error is not null) return Problem(statusCode: error.StatusCode, title: error.Message);

        return await attendance.GetDayAsync(date);
    }
}
