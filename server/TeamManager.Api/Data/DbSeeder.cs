using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeamManager.Api.Entities;

namespace TeamManager.Api.Data;

/// <summary>
/// מריץ מיגרציות (אם הוגדר) ויוצר את שתי המשתמשות הראשונות מתוך הקונפיגורציה,
/// רק כשטבלת Users ריקה.
/// </summary>
public static class DbSeeder
{
    private record InitialUser(string Username, string Password, string Email, UserRole Role, string? DisplayName = null);

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (config.GetValue<bool>("Database:MigrateOnStartup"))
            await db.Database.MigrateAsync();

        if (await db.Users.AnyAsync()) return;

        var initialUsers = config.GetSection("InitialUsers").Get<List<InitialUser>>() ?? [];
        if (initialUsers.Count == 0)
        {
            logger.LogWarning("No users exist and no InitialUsers are configured; nobody can log in yet.");
            return;
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        foreach (var u in initialUsers)
        {
            var user = new User { Username = u.Username, DisplayName = u.DisplayName, Email = u.Email, Role = u.Role };
            user.PasswordHash = hasher.HashPassword(user, u.Password);
            db.Users.Add(user);
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Created {Count} initial users.", initialUsers.Count);
    }
}
