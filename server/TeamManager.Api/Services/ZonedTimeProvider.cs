namespace TeamManager.Api.Services;

/// <summary>
/// שעון שה"זמן המקומי" שלו הוא אזור הזמן של הצוות (App:TimeZone), ולא של השרת.
/// באחסון חיצוני השרת יכול לשבת באזור זמן אחר, ואז תזכורות, "היום" וסוף החודש היו זזים.
/// </summary>
public class ZonedTimeProvider(TimeZoneInfo zone) : TimeProvider
{
    public override TimeZoneInfo LocalTimeZone => zone;
}
