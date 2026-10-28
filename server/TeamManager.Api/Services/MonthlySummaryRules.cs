using System.Globalization;
using System.Net;
using System.Text;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;

namespace TeamManager.Api.Services;

/// <summary>חישוב הסיכום החודשי ותוכן המייל, בלי תלות במסד נתונים.</summary>
public static class MonthlySummaryRules
{
    /// <summary>שעת שליחת הסיכום האוטומטי ב-1 לחודש.</summary>
    public static readonly TimeOnly SendTime = new(8, 0);

    private static readonly CultureInfo Hebrew = CultureInfo.GetCultureInfo("he-IL");

    /// <summary>
    /// שורה לכל עובדת פעילה, ולכל עובדת לא פעילה שיש לה נתונים בחודש.
    /// סטטוסים וממוצע רק מימים שבהם העובדת סומנה נוכחת והוזן ביצוע; ימי היעדרות לא נספרים בממוצע.
    /// ימים חלקיים נספרים מתוך ימי הביצוע (לפי אחוז העבודה שנשמר באותו יום).
    /// </summary>
    public static List<MonthlySummaryRowDto> BuildRows(
        IEnumerable<Employee> employees,
        IReadOnlyCollection<Attendance> attendance,
        IReadOnlyCollection<DailyStatus> statuses)
    {
        var attendanceByEmployee = attendance.ToLookup(a => a.EmployeeId);
        var statusesByEmployee = statuses.ToLookup(s => s.EmployeeId);

        return employees
            .Where(e => e.IsActive || attendanceByEmployee.Contains(e.Id) || statusesByEmployee.Contains(e.Id))
            .OrderBy(e => e.Name)
            .Select(e =>
            {
                var att = attendanceByEmployee[e.Id].ToList();
                var presentDates = att.Where(a => a.Status == AttendanceStatus.Present).Select(a => a.Date).ToHashSet();
                var st = statusesByEmployee[e.Id].Where(s => presentDates.Contains(s.Date)).ToList();

                return new MonthlySummaryRowDto(
                    e.Id,
                    e.Name,
                    e.IsActive,
                    PresentDays: presentDates.Count,
                    PartialDays: st.Count(s => s.WorkPercent < 100),
                    VacationDays: att.Count(a => a.Status == AttendanceStatus.Vacation),
                    SickDays: att.Count(a => a.Status == AttendanceStatus.Sick),
                    OtherDays: att.Count(a => a.Status == AttendanceStatus.Other),
                    WeakDays: st.Count(s => s.Rating == Rating.Weak),
                    GoodDays: st.Count(s => s.Rating == Rating.Good),
                    ExcellentDays: st.Count(s => s.Rating == Rating.Excellent),
                    DaysWithoutOutput: presentDates.Count - st.Count,
                    AveragePercent: Average(st));
            })
            .ToList();
    }

    /// <summary>ממוצע על כל ימי הביצוע של הצוות (לא ממוצע של ממוצעים).</summary>
    public static decimal? TeamAverage(IReadOnlyCollection<DailyStatus> statuses, IReadOnlyCollection<Attendance> attendance)
    {
        var present = attendance
            .Where(a => a.Status == AttendanceStatus.Present)
            .Select(a => (a.EmployeeId, a.Date))
            .ToHashSet();
        return Average(statuses.Where(s => present.Contains((s.EmployeeId, s.Date))).ToList());
    }

    private static decimal? Average(IReadOnlyCollection<DailyStatus> statuses) =>
        statuses.Count == 0 ? null : Math.Round(statuses.Average(s => s.Percent), 2, MidpointRounding.AwayFromZero);

    public static (int Year, int Month) PreviousMonth(DateOnly today)
    {
        var d = today.AddMonths(-1);
        return (d.Year, d.Month);
    }

    /// <summary>
    /// הסיכום של החודש הקודם יוצא מ-1 לחודש בשעה 08:00. אם השרת לא רץ בדיוק אז, הוא יוצא כשהשרת עולה
    /// (הבדיקה אם כבר נשלח נעשית בשירות).
    /// </summary>
    public static bool IsSummaryDue(DateTime localNow) =>
        localNow.Day > 1 || TimeOnly.FromDateTime(localNow) >= SendTime;

    public static string MonthName(int year, int month) => new DateTime(year, month, 1).ToString("MMMM yyyy", Hebrew);

    public static string Subject(int year, int month) => $"סיכום חודשי – {MonthName(year, month)}";

    public static string Html(MonthlySummaryDto s, string? link)
    {
        static string Encode(string v) => WebUtility.HtmlEncode(v);
        static string Pct(decimal? p) => p is { } v ? v.ToString("0.00", CultureInfo.InvariantCulture) + "%" : "—";

        var html = new StringBuilder();
        html.Append("<div dir=\"rtl\" style=\"font-family:Arial,sans-serif\">");
        html.Append($"<h2>סיכום חודשי – {Encode(MonthName(s.Year, s.Month))}</h2>");
        html.Append($"<p>ממוצע עמידה ביעד של הצוות: <b>{Pct(s.TeamAveragePercent)}</b></p>");
        html.Append("<table border=\"1\" cellpadding=\"6\" style=\"border-collapse:collapse\">");
        html.Append("<tr><th>שם</th><th>ימי נוכחות</th><th>חופשה</th><th>מחלה</th><th>אחר</th>" +
                    "<th>חלש</th><th>טוב</th><th>מעולה</th><th>ממוצע</th></tr>");
        foreach (var r in s.Rows)
        {
            html.Append("<tr>");
            html.Append($"<td>{Encode(r.EmployeeName)}</td>");
            html.Append($"<td>{r.PresentDays}{(r.PartialDays > 0 ? $" ({r.PartialDays} חלקיים)" : "")}</td>");
            html.Append($"<td>{r.VacationDays}</td><td>{r.SickDays}</td><td>{r.OtherDays}</td>");
            html.Append($"<td>{r.WeakDays}</td><td>{r.GoodDays}</td><td>{r.ExcellentDays}</td>");
            html.Append($"<td>{Pct(r.AveragePercent)}</td>");
            html.Append("</tr>");
        }
        html.Append("</table>");

        var missing = s.Rows.Sum(r => r.DaysWithoutOutput);
        if (missing > 0)
            html.Append($"<p>שימי לב: {missing} ימי נוכחות בלי ביצוע, והם לא נכללים בממוצע.</p>");

        html.Append("<p>הפירוט המלא בקובץ האקסל המצורף.</p>");
        if (!string.IsNullOrWhiteSpace(link))
            html.Append($"<p><a href=\"{Encode(link)}\">למסך הבונוסים</a></p>");
        html.Append("</div>");
        return html.ToString();
    }
}
