using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;
using TeamManager.Api.Services;

namespace TeamManager.Api.Controllers;

/// <summary>בונוסים חודשיים: מנהלת הצוות ממלאת ושולחת לשכר; המנהלת צופה.</summary>
[ApiController]
[Route("api/bonuses/{year:int}/{month:int}")]
[Authorize]
public class BonusesController(BonusService bonuses, SettingsService settings, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BonusMonthDto>> Get(int year, int month)
    {
        if (InvalidMonth(year, month) is { } problem) return problem;
        return await bonuses.GetAsync(year, month);
    }

    [HttpGet("excel")]
    public async Task<IActionResult> Excel(int year, int month)
    {
        if (InvalidMonth(year, month) is { } problem) return problem;
        var data = await bonuses.GetAsync(year, month);
        return File(BonusRules.Excel(data), MonthlySummaryExcel.ContentType, BonusRules.ExcelFileName(year, month));
    }

    [HttpPut]
    [Authorize(Roles = nameof(UserRole.TeamLead))]
    public async Task<ActionResult<BonusMonthDto>> Save(int year, int month, BonusSaveRequest req)
    {
        if (InvalidMonth(year, month) is { } problem) return problem;

        var cap = (await settings.GetAsync()).BonusCap;
        var errors = BonusRules.Validate(req.Entries, cap);
        if (errors.Count > 0) return ValidationProblem(new ValidationProblemDetails(errors));

        var error = await bonuses.SaveAsync(year, month, req.Entries);
        if (error is not null) return Problem(statusCode: error.StatusCode, title: error.Message);
        return await bonuses.GetAsync(year, month);
    }

    /// <summary>שליחה לשכר: מייל למנהלת עם טבלה ואקסל, ונעילת החודש.</summary>
    [HttpPost("send")]
    [Authorize(Roles = nameof(UserRole.TeamLead))]
    public async Task<ActionResult<BonusMonthDto>> Send(int year, int month)
    {
        if (InvalidMonth(year, month) is { } problem) return problem;

        var error = await bonuses.SendAndLockAsync(year, month, User.FindFirstValue(ClaimTypes.Name)!);
        if (error is not null) return Problem(statusCode: error.StatusCode, title: error.Message);
        return await bonuses.GetAsync(year, month);
    }

    private ObjectResult? InvalidMonth(int year, int month)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        if (month is < 1 or > 12 || year < 2000 || new DateOnly(year, month, 1) > today)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "חודש לא תקין");
        return null;
    }
}
