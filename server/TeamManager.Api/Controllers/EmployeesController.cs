using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamManager.Api.Data;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;

namespace TeamManager.Api.Controllers;

/// <summary>
/// ניהול עובדות. עובדת שעזבה מסומנת לא פעילה.
/// מחיקה מעבירה לארכיון: העובדת נעלמת מהרשימות הרגילות, ההיסטוריה נשמרת ואפשר לשחזר.
/// </summary>
[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<EmployeeDto>> List([FromQuery] bool includeInactive = false, [FromQuery] bool archived = false)
    {
        var query = db.Employees.AsNoTracking();
        if (archived) query = query.Where(e => e.DeletedAt != null);
        else query = query.Where(e => e.DeletedAt == null && (includeInactive || e.IsActive));

        var employees = await query
            .OrderByDescending(e => e.IsActive)
            .ThenBy(e => e.Name)
            .ToListAsync();
        return employees.Select(EmployeeDto.From).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmployeeDto>> Get(int id)
    {
        var employee = await db.Employees.FindAsync(id);
        return employee is null ? NotFound() : EmployeeDto.From(employee);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.TeamLead))]
    public async Task<ActionResult<EmployeeDto>> Create(EmployeeUpsertRequest req)
    {
        var employee = new Employee
        {
            Name = req.Name.Trim(),
            DailyTarget = req.DailyTarget,
            IsActive = req.IsActive,
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = employee.Id }, EmployeeDto.From(employee));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(UserRole.TeamLead))]
    public async Task<ActionResult<EmployeeDto>> Update(int id, EmployeeUpsertRequest req)
    {
        var employee = await db.Employees.FindAsync(id);
        if (employee is null) return NotFound();
        if (employee.DeletedAt is not null)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "העובדת בארכיון, צריך לשחזר אותה לפני עריכה");

        // שינוי יעד לא משנה נתוני עבר: היעד נשמר בכל רשומת DailyStatus בנפרד.
        employee.Name = req.Name.Trim();
        employee.DailyTarget = req.DailyTarget;
        employee.IsActive = req.IsActive;
        await db.SaveChangesAsync();
        return EmployeeDto.From(employee);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = nameof(UserRole.TeamLead))]
    public async Task<IActionResult> Delete(int id)
    {
        var employee = await db.Employees.FindAsync(id);
        if (employee is null) return NotFound();

        // לא פעילה כדי שהשירותים (נוכחות, ביצוע, תזכורות) יתייחסו אליה כמו לעובדת שעזבה.
        employee.IsActive = false;
        employee.DeletedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/restore")]
    [Authorize(Roles = nameof(UserRole.TeamLead))]
    public async Task<ActionResult<EmployeeDto>> Restore(int id)
    {
        var employee = await db.Employees.FindAsync(id);
        if (employee is null) return NotFound();

        employee.DeletedAt = null;
        employee.IsActive = true;
        await db.SaveChangesAsync();
        return EmployeeDto.From(employee);
    }
}
