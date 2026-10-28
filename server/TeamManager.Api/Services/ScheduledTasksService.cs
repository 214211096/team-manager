namespace TeamManager.Api.Services;

/// <summary>
/// משימות מתוזמנות: פעם בדקה בודק אם צריך לשלוח תזכורת נוכחות או סיכום חודשי.
/// כל משימה רצה בנפרד, כך שכשל באחת לא עוצר את השנייה.
/// </summary>
public class ScheduledTasksService(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<ScheduledTasksService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        do
        {
            var now = clock.GetLocalNow().DateTime;
            await RunAsync("attendance reminder", sp => sp.GetRequiredService<AttendanceService>().SendReminderIfDueAsync(now, stoppingToken));
            await RunAsync("monthly summary", sp => sp.GetRequiredService<MonthlySummaryService>().SendIfDueAsync(now, stoppingToken));
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunAsync(string name, Func<IServiceProvider, Task> task)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await task(scope.ServiceProvider);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Scheduled task '{Task}' failed", name);
        }
    }
}
