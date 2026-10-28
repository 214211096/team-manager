using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;
using TeamManager.Api.Services;

namespace TeamManager.Api.Controllers;

/// <summary>לוח שנה: היעדרויות וימים חלקיים מראש. צפייה לשתי המשתמשות, עריכה למנהלת הצוות.</summary>
[ApiController]
[Route("api/schedule")]
[Authorize]
public class ScheduleController(ScheduleService schedule) : ControllerBase
{
    /// <summary>כל הרישומים שחופפים לטווח (למשל חודש שמוצג בלוח).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScheduleEntryDto>>> List([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        if (to < from || to.DayNumber - from.DayNumber > 100)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "טווח תאריכים לא תקין");
        return Ok(await schedule.ListAsync(from, to));
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.TeamLead))]
    public Task<ActionResult<ScheduleEntryDto>> Create(ScheduleEntryRequest req) => Save(null, req);

    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(UserRole.TeamLead))]
    public Task<ActionResult<ScheduleEntryDto>> Update(int id, ScheduleEntryRequest req) => Save(id, req);

    [HttpDelete("{id:int}")]
    [Authorize(Roles = nameof(UserRole.TeamLead))]
    public async Task<IActionResult> Delete(int id)
    {
        var error = await schedule.DeleteAsync(id);
        return error is null ? NoContent() : Problem(statusCode: error.StatusCode, title: error.Message);
    }

    private async Task<ActionResult<ScheduleEntryDto>> Save(int? id, ScheduleEntryRequest req)
    {
        var errors = ScheduleRules.Validate(req);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        var (entry, error) = await schedule.SaveAsync(id, req);
        if (error is not null) return Problem(statusCode: error.StatusCode, title: error.Message);
        return entry!;
    }
}
