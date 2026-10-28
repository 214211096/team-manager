using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TeamManager.Api.Data;
using TeamManager.Api.Entities;

namespace TeamManager.Api.Services;

/// <summary>הגדרת כלי לעוזר: שם, תיאור ו-JSON Schema של הקלט.</summary>
public record AssistantToolDefinition(string Name, string Description, Dictionary<string, object> Properties, string[] Required);

/// <summary>
/// הכלים שהעוזר יכול להפעיל. כולם לקריאה בלבד: אין לעוזר שום דרך לשנות נתונים.
/// כל כלי מחזיר JSON קומפקטי, ושגיאת קלט חוזרת כטקסט כדי שהמודל יתקן את הבקשה.
/// </summary>
public class AssistantTools(
    AppDbContext db,
    MonthlySummaryService monthlySummary,
    BonusService bonuses,
    ScheduleService schedule)
{
    public const int MaxRangeDays = 366;

    private static readonly JsonSerializerOptions Json = new()
    {
        // עברית כמו שהיא (בלי א...), כדי לחסוך טוקנים.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly Dictionary<string, object> DateRange = new()
    {
        ["from"] = new { type = "string", description = "תאריך התחלה, yyyy-MM-dd (כולל)" },
        ["to"] = new { type = "string", description = "תאריך סיום, yyyy-MM-dd (כולל). טווח של עד שנה." },
        ["employee_name"] = new { type = "string", description = "אופציונלי: שם העובדת או חלק ממנו, לסינון" },
    };

    private static readonly Dictionary<string, object> YearMonth = new()
    {
        ["year"] = new { type = "integer", description = "שנה, למשל 2026" },
        ["month"] = new { type = "integer", description = "חודש 1-12" },
    };

    public static readonly IReadOnlyList<AssistantToolDefinition> Definitions =
    [
        new("list_employees",
            "רשימת העובדות: מזהה, שם, יעד יומי נוכחי, והאם פעילה. כדאי להתחיל כאן כדי לזהות שם מדויק.",
            new() { ["include_inactive"] = new { type = "boolean", description = "לכלול גם עובדות שעזבו" } },
            []),
        new("get_attendance",
            "רשומות נוכחות לפי יום: Present (נוכחת), Vacation (חופשה), Sick (מחלה), Other (אחר, עם סיבה). " +
            "ימים בלי רשומה לא דווחו. לשאלות כמו 'באילו ימים X הייתה חסרה'.",
            DateRange, ["from", "to"]),
        new("get_daily_status",
            "ביצוע יומי מול יעד, רק לימים שבהם העובדת נכחה והוזן ביצוע: יעד, ביצוע, אחוז, דירוג " +
            "(Weak חלש / Good טוב / Excellent מעולה), ואחוז עבודה ביום חלקי.",
            DateRange, ["from", "to"]),
        new("get_monthly_summary",
            "סיכום חודשי לכל עובדת: ימי נוכחות, ימים חלקיים, היעדרויות לפי סוג, ימים בכל דירוג, ימים בלי ביצוע, " +
            "ממוצע עמידה ביעד, וממוצע צוות.",
            YearMonth, ["year", "month"]),
        new("get_bonuses",
            "בונוסים של חודש: סכום והערה לכל עובדת (null אם עוד לא הוזן), סך הכול, והאם החודש נשלח לשכר ונעול.",
            YearMonth, ["year", "month"]),
        new("get_calendar",
            "רישומים מראש בלוח השנה שחופפים לטווח: היעדרויות מתוכננות וימי עבודה חלקיים (אחוז).",
            DateRange, ["from", "to"]),
    ];

    public async Task<string> ExecuteAsync(string name, IReadOnlyDictionary<string, JsonElement> input)
    {
        try
        {
            object result = name switch
            {
                "list_employees" => await ListEmployeesAsync(Bool(input, "include_inactive")),
                "get_attendance" => await AttendanceAsync(Range(input), Str(input, "employee_name")),
                "get_daily_status" => await DailyStatusAsync(Range(input), Str(input, "employee_name")),
                "get_monthly_summary" => await MonthlySummaryAsync(Month(input)),
                "get_bonuses" => await BonusesAsync(Month(input)),
                "get_calendar" => await CalendarAsync(Range(input), Str(input, "employee_name")),
                _ => throw new ArgumentException($"כלי לא מוכר: {name}"),
            };
            return JsonSerializer.Serialize(result, Json);
        }
        catch (ArgumentException ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, Json);
        }
    }

    private async Task<object> ListEmployeesAsync(bool includeInactive) =>
        await db.Employees.AsNoTracking()
            .Where(e => includeInactive || e.IsActive)
            .OrderBy(e => e.Name)
            .Select(e => new { e.Id, e.Name, e.DailyTarget, e.IsActive })
            .ToListAsync();

    private async Task<object> AttendanceAsync((DateOnly From, DateOnly To) r, string? employee)
    {
        var query = db.Attendance.AsNoTracking().Where(a => a.Date >= r.From && a.Date <= r.To);
        if (employee is not null) query = query.Where(a => a.Employee.Name.Contains(employee));
        return await query
            .OrderBy(a => a.Date).ThenBy(a => a.Employee.Name)
            .Select(a => new { Date = a.Date.ToString("yyyy-MM-dd"), Employee = a.Employee.Name, a.Status, a.OtherReason })
            .ToListAsync();
    }

    private async Task<object> DailyStatusAsync((DateOnly From, DateOnly To) r, string? employee)
    {
        var query = db.DailyStatuses.AsNoTracking().Where(d => d.Date >= r.From && d.Date <= r.To);
        if (employee is not null) query = query.Where(d => d.Employee.Name.Contains(employee));
        return await query
            .OrderBy(d => d.Date).ThenBy(d => d.Employee.Name)
            .Select(d => new
            {
                Date = d.Date.ToString("yyyy-MM-dd"),
                Employee = d.Employee.Name,
                Target = d.TargetSnapshot,
                d.Completed,
                d.Percent,
                d.Rating,
                WorkPercent = d.WorkPercent == 100 ? (int?)null : d.WorkPercent,
            })
            .ToListAsync();
    }

    private async Task<object> MonthlySummaryAsync((int Year, int Month) m) =>
        await monthlySummary.BuildAsync(m.Year, m.Month);

    private async Task<object> BonusesAsync((int Year, int Month) m)
    {
        var b = await bonuses.GetAsync(m.Year, m.Month);
        return new
        {
            b.Year,
            b.Month,
            SentToPayroll = b.Locked,
            b.LockedAt,
            b.BonusCap,
            b.Total,
            Rows = b.Rows.Select(r => new { Employee = r.Summary.EmployeeName, r.Amount, r.Note }),
        };
    }

    private async Task<object> CalendarAsync((DateOnly From, DateOnly To) r, string? employee)
    {
        var entries = await schedule.ListAsync(r.From, r.To);
        return entries
            .Where(e => employee is null || e.EmployeeName.Contains(employee))
            .Select(e => new
            {
                Employee = e.EmployeeName,
                StartDate = e.StartDate.ToString("yyyy-MM-dd"),
                EndDate = e.EndDate.ToString("yyyy-MM-dd"),
                e.Kind,
                e.AbsenceStatus,
                e.Reason,
                e.WorkPercent,
            });
    }

    private static string? Str(IReadOnlyDictionary<string, JsonElement> input, string key) =>
        input.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;

    private static bool Bool(IReadOnlyDictionary<string, JsonElement> input, string key) =>
        input.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.True;

    private static DateOnly Date(IReadOnlyDictionary<string, JsonElement> input, string key) =>
        Str(input, key) is { } s && DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : throw new ArgumentException($"'{key}' חייב להיות תאריך בפורמט yyyy-MM-dd");

    private static (DateOnly From, DateOnly To) Range(IReadOnlyDictionary<string, JsonElement> input)
    {
        var from = Date(input, "from");
        var to = Date(input, "to");
        if (to < from) throw new ArgumentException("'to' לפני 'from'");
        if (to.DayNumber - from.DayNumber >= MaxRangeDays) throw new ArgumentException("טווח של עד שנה; פצל לכמה בקשות");
        return (from, to);
    }

    private static (int Year, int Month) Month(IReadOnlyDictionary<string, JsonElement> input)
    {
        if (!input.TryGetValue("year", out var y) || !y.TryGetInt32(out var year) || year is < 2000 or > 2100)
            throw new ArgumentException("'year' לא תקין");
        if (!input.TryGetValue("month", out var m) || !m.TryGetInt32(out var month) || month is < 1 or > 12)
            throw new ArgumentException("'month' חייב להיות 1-12");
        return (year, month);
    }
}
