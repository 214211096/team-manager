using System.Globalization;
using System.Net;
using System.Text;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;

namespace TeamManager.Api.Services;

/// <summary>כללי הנוכחות ותוכן המיילים, בלי תלות במסד נתונים כדי שאפשר יהיה לבדוק אותם.</summary>
public static class AttendanceRules
{
    public const int OtherReasonMaxLength = 200;

    private static readonly CultureInfo Hebrew = CultureInfo.GetCultureInfo("he-IL");

    public static readonly IReadOnlyDictionary<AttendanceStatus, string> StatusLabels =
        new Dictionary<AttendanceStatus, string>
        {
            [AttendanceStatus.Present] = "נוכחת",
            [AttendanceStatus.Vacation] = "חופשה",
            [AttendanceStatus.Sick] = "מחלה",
            [AttendanceStatus.Other] = "אחר",
        };

    /// <summary>ימי עבודה: ראשון עד חמישי, בלי חגים (מזוהים אוטומטית).</summary>
    public static bool IsWorkday(DateOnly date) => HolidayCalendar.IsWorkday(date);

    /// <summary>התזכורת יוצאת ביום עבודה שלא הוגדר בהגדרות כיום חופש נוסף, החל משעת התזכורת.</summary>
    public static bool IsReminderDue(DateTime localNow, TimeOnly reminderTime, IReadOnlyCollection<DateOnly> noReminderDates)
    {
        var today = DateOnly.FromDateTime(localNow);
        return IsWorkday(today) && !noReminderDates.Contains(today) && TimeOnly.FromDateTime(localNow) >= reminderTime;
    }

    /// <summary>מחזיר שגיאות לפי שדה (בפורמט של ValidationProblemDetails); ריק אם הכול תקין.</summary>
    public static Dictionary<string, string[]> Validate(IReadOnlyList<AttendanceEntryRequest> entries)
    {
        var errors = new Dictionary<string, string[]>();

        if (entries.Select(e => e.EmployeeId).Distinct().Count() != entries.Count)
            errors["entries"] = ["עובדת מופיעה יותר מפעם אחת"];

        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (!Enum.IsDefined(e.Status))
                errors[$"entries[{i}].status"] = ["סטטוס לא תקין"];
            else if (e.Status == AttendanceStatus.Other && string.IsNullOrWhiteSpace(e.OtherReason))
                errors[$"entries[{i}].otherReason"] = ["בסטטוס 'אחר' חובה לכתוב סיבה"];
            else if (e.OtherReason?.Trim().Length > OtherReasonMaxLength)
                errors[$"entries[{i}].otherReason"] = [$"סיבה עד {OtherReasonMaxLength} תווים"];
        }

        return errors;
    }

    public static string FormatDate(DateOnly date) => date.ToString("dddd, dd'/'MM'/'yyyy", Hebrew);

    public static string ReportSubject(DateOnly date, bool isUpdate) =>
        $"{(isUpdate ? "עדכון: " : "")}דיווח נוכחות – {FormatDate(date)}";

    /// <summary>"נוכחת", או סוג ההיעדרות, ובסטטוס "אחר" גם הסיבה.</summary>
    public static string Describe(AttendanceRowDto r) =>
        r.Status is AttendanceStatus.Other ? $"אחר – {r.OtherReason}"
        : r.Status is { } s ? StatusLabels[s]
        : "";

    /// <summary>
    /// גוף הדיווח: X מתוך Y נוכחות ורשימת החסרות עם הסיבה.
    /// בעדכון (changed לא null) מופיעות קודם העובדות שהסטטוס שלהן השתנה מאז השליחה הקודמת.
    /// </summary>
    public static string ReportHtml(DateOnly date, IReadOnlyList<AttendanceRowDto> rows, IReadOnlyList<AttendanceRowDto>? changed)
    {
        var present = rows.Count(r => r.Status == AttendanceStatus.Present);
        var absent = rows.Where(r => r.Status is not AttendanceStatus.Present).ToList();

        var html = new StringBuilder();
        html.Append("<div dir=\"rtl\" style=\"font-family:Arial,sans-serif\">");
        html.Append($"<h2>דיווח נוכחות – {Encode(FormatDate(date))}</h2>");

        if (changed is not null)
        {
            html.Append("<p><b>עדכון לדיווח שנשלח קודם לכן.</b></p>");
            if (changed.Count > 0)
            {
                html.Append("<p>מה השתנה:</p><ul>");
                foreach (var r in changed) html.Append($"<li>{Encode(r.EmployeeName)}: {Encode(Describe(r))}</li>");
                html.Append("</ul>");
            }
        }

        html.Append($"<p><b>נוכחות: {present} מתוך {rows.Count}</b></p>");

        var partial = rows
            .Where(r => r is { Status: AttendanceStatus.Present, Planned: { Kind: ScheduleKind.PartialDay, WorkPercent: not null } })
            .ToList();
        if (partial.Count > 0)
            html.Append("<p>עבודה חלקית: " +
                Encode(string.Join(", ", partial.Select(r => $"{r.EmployeeName} ({r.Planned!.WorkPercent}%)"))) + "</p>");
        if (absent.Count == 0)
        {
            html.Append("<p>כל העובדות נוכחות.</p>");
        }
        else
        {
            html.Append("<p>חסרות:</p>");
            html.Append("<table border=\"1\" cellpadding=\"6\" style=\"border-collapse:collapse\">");
            html.Append("<tr><th>שם</th><th>סיבה</th></tr>");
            foreach (var r in absent)
                html.Append($"<tr><td>{Encode(r.EmployeeName)}</td><td>{Encode(Describe(r))}</td></tr>");
            html.Append("</table>");
        }

        html.Append("</div>");
        return html.ToString();
    }

    public const string ReminderSubject = "תזכורת: דיווח הנוכחות של היום עוד לא נשלח";

    public static string ReminderHtml(DateOnly date) =>
        "<div dir=\"rtl\" style=\"font-family:Arial,sans-serif\">" +
        $"<p>דיווח הנוכחות ל{Encode(FormatDate(date))} עוד לא נשלח למנהלת.</p>" +
        "<p>אפשר למלא ולשלוח אותו במסך 'נוכחות יומית' במערכת.</p></div>";

    private static string Encode(string s) => WebUtility.HtmlEncode(s);
}
