using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;

namespace TeamManager.Api.Services;

/// <summary>כללי לוח השנה, בלי תלות במסד נתונים.</summary>
public static class ScheduleRules
{
    public const int MaxRangeDays = 366;
    public const int ReasonMaxLength = AttendanceRules.OtherReasonMaxLength;

    private static readonly AttendanceStatus[] AbsenceStatuses =
        [AttendanceStatus.Vacation, AttendanceStatus.Sick, AttendanceStatus.Other];

    public static Dictionary<string, string[]> Validate(ScheduleEntryRequest r)
    {
        var errors = new Dictionary<string, string[]>();

        if (r.EndDate < r.StartDate)
            errors["endDate"] = ["תאריך הסיום לפני תאריך ההתחלה"];
        else if (r.EndDate.DayNumber - r.StartDate.DayNumber >= MaxRangeDays)
            errors["endDate"] = ["טווח של עד שנה"];

        switch (r.Kind)
        {
            case ScheduleKind.Absence:
                if (r.AbsenceStatus is not { } s || !AbsenceStatuses.Contains(s))
                    errors["absenceStatus"] = ["יש לבחור סוג היעדרות"];
                else if (s == AttendanceStatus.Other && string.IsNullOrWhiteSpace(r.Reason))
                    errors["reason"] = ["בהיעדרות מסוג 'אחר' חובה לכתוב סיבה"];
                break;
            case ScheduleKind.PartialDay:
                if (r.WorkPercent is not (>= 1 and <= 99))
                    errors["workPercent"] = ["אחוז עבודה בין 1 ל-99"];
                break;
            default:
                errors["kind"] = ["סוג רישום לא תקין"];
                break;
        }

        if (r.Reason?.Trim().Length > ReasonMaxLength)
            errors["reason"] = [$"סיבה עד {ReasonMaxLength} תווים"];

        return errors;
    }

    /// <summary>היעד בפועל ביום חלקי: היעד המלא × אחוז העבודה, מעוגל, ולפחות 1.</summary>
    public static int EffectiveTarget(int baseTarget, int workPercent) =>
        Math.Max(1, (int)Math.Round(baseTarget * workPercent / 100m, MidpointRounding.AwayFromZero));

    public static PlannedDayDto ToPlanned(ScheduleEntry e) => new(e.Kind, e.AbsenceStatus, e.Reason, e.WorkPercent);

    /// <summary>אחוז העבודה ביום לפי הרישום (100 אם אין רישום חלקי).</summary>
    public static int WorkPercentFor(ScheduleEntry? e) =>
        e is { Kind: ScheduleKind.PartialDay, WorkPercent: { } p } ? p : 100;
}
