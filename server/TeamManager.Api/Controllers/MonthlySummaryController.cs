using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;
using TeamManager.Api.Services;

namespace TeamManager.Api.Controllers;

/// <summary>סיכום חודשי: צפייה והורדת אקסל לשתי המשתמשות, שליחה במייל למנהלת הצוות.</summary>
[ApiController]
[Route("api/monthly-summary/{year:int}/{month:int}")]
[Authorize]
public class MonthlySummaryController(MonthlySummaryService summary, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MonthlySummaryDto>> Get(int year, int month)
    {
        if (InvalidMonth(year, month) is { } problem) return problem;
        return await summary.BuildAsync(year, month);
    }

    [HttpGet("excel")]
    public async Task<IActionResult> Excel(int year, int month)
    {
        if (InvalidMonth(year, month) is { } problem) return problem;
        return File(await summary.BuildExcelAsync(year, month), MonthlySummaryExcel.ContentType, MonthlySummaryExcel.FileName(year, month));
    }

    [HttpPost("send")]
    [Authorize(Roles = nameof(UserRole.TeamLead))]
    public async Task<ActionResult<MonthlySummaryDto>> Send(int year, int month)
    {
        if (InvalidMonth(year, month) is { } problem) return problem;
        var error = await summary.SendAsync(year, month);
        if (error is not null) return Problem(statusCode: error.StatusCode, title: error.Message);
        return await summary.BuildAsync(year, month);
    }

    private ObjectResult? InvalidMonth(int year, int month)
    {
        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        if (month is < 1 or > 12 || year < 2000 || new DateOnly(year, month, 1) > today)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "חודש לא תקין");
        return null;
    }
}
