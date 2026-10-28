using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TeamManager.Api.Data;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;

namespace TeamManager.Api.Services;

/// <summary>קריאה וכתיבה של הגדרות המערכת מטבלת Settings, עם ערכי ברירת מחדל.</summary>
public class SettingsService(AppDbContext db)
{
    private static class Keys
    {
        public const string ManagerEmail = "ManagerEmail";
        public const string ReminderTime = "ReminderTime";
        public const string WeakThreshold = "WeakThreshold";
        public const string ExcellentThreshold = "ExcellentThreshold";
        public const string BonusCap = "BonusCap";
        public const string NoReminderDates = "NoReminderDates";
    }

    public static readonly AppSettingsDto Defaults = new()
    {
        ManagerEmail = "",
        ReminderTime = "09:30",
        WeakThreshold = 80,
        ExcellentThreshold = 90,
        BonusCap = 600,
        NoReminderDates = [],
    };

    public async Task<AppSettingsDto> GetAsync()
    {
        var values = await db.Settings.ToDictionaryAsync(s => s.Key, s => s.Value);

        string Str(string key, string fallback) => values.GetValueOrDefault(key) ?? fallback;
        decimal Dec(string key, decimal fallback) =>
            decimal.TryParse(values.GetValueOrDefault(key), CultureInfo.InvariantCulture, out var v) ? v : fallback;

        return new AppSettingsDto
        {
            ManagerEmail = Str(Keys.ManagerEmail, Defaults.ManagerEmail),
            ReminderTime = Str(Keys.ReminderTime, Defaults.ReminderTime),
            WeakThreshold = Dec(Keys.WeakThreshold, Defaults.WeakThreshold),
            ExcellentThreshold = Dec(Keys.ExcellentThreshold, Defaults.ExcellentThreshold),
            BonusCap = Dec(Keys.BonusCap, Defaults.BonusCap),
            NoReminderDates = ParseDates(values.GetValueOrDefault(Keys.NoReminderDates)),
        };
    }

    public async Task SaveAsync(AppSettingsDto dto)
    {
        var pairs = new Dictionary<string, string>
        {
            [Keys.ManagerEmail] = dto.ManagerEmail.Trim(),
            [Keys.ReminderTime] = dto.ReminderTime,
            [Keys.WeakThreshold] = dto.WeakThreshold.ToString(CultureInfo.InvariantCulture),
            [Keys.ExcellentThreshold] = dto.ExcellentThreshold.ToString(CultureInfo.InvariantCulture),
            [Keys.BonusCap] = dto.BonusCap.ToString(CultureInfo.InvariantCulture),
            [Keys.NoReminderDates] = string.Join(",", dto.NoReminderDates
                .Distinct()
                .Order()
                .Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))),
        };

        var existing = await db.Settings.Where(s => pairs.Keys.Contains(s.Key)).ToListAsync();
        foreach (var (key, value) in pairs)
        {
            var row = existing.FirstOrDefault(s => s.Key == key);
            if (row is null) db.Settings.Add(new Setting { Key = key, Value = value });
            else row.Value = value;
        }
        await db.SaveChangesAsync();
    }

    /// <summary>רשימת תאריכים מופרדת בפסיקים (yyyy-MM-dd); ערכים לא תקינים מדולגים.</summary>
    private static List<DateOnly> ParseDates(string? value) =>
        (value ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : (DateOnly?)null)
            .OfType<DateOnly>()
            .ToList();
}
