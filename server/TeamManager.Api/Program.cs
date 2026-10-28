using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeamManager.Api.Controllers;
using TeamManager.Api.Data;
using TeamManager.Api.Entities;
using TeamManager.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// מסד מרוחק (באחסון חיצוני) יכול להתנתק לרגע; ניסיון חוזר אוטומטי.
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default"), sql => sql.EnableRetryOnFailure()));

builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<DailyStatusService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<MonthlySummaryService>();
builder.Services.AddScoped<BonusService>();
builder.Services.AddScoped<AssistantTools>();
builder.Services.AddScoped<AssistantService>();
builder.Services.Configure<AssistantOptions>(builder.Configuration.GetSection("Assistant"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<TimeProvider>(new ZonedTimeProvider(
    TimeZoneInfo.FindSystemTimeZoneById(builder.Configuration["App:TimeZone"] ?? "Asia/Jerusalem")));
builder.Services.AddHostedService<ScheduledTasksService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// התחברות בעוגייה מוצפנת (HttpOnly), כך שהלקוח לא מחזיק טוקן ב-JavaScript.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "tm_auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        // בפיתוח האתר רץ ב-http; בכל סביבה אחרת העוגייה נשלחת רק ב-HTTPS.
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        o.ExpireTimeSpan = TimeSpan.FromHours(10);
        o.SlidingExpiration = true;
        // API: מחזירים 401/403 במקום הפניה לדף התחברות.
        o.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

// הגנה מניחוש סיסמאות: עד 5 ניסיונות בדקה מכל כתובת IP, בכניסה ובשינוי סיסמה.
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy(AuthController.RateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
    // עוזר ה-AI: עד 20 שאלות בדקה לכל משתמשת, כדי להגביל עלויות.
    o.AddPolicy(AssistantController.RateLimitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
    o.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new { title = "יותר מדי ניסיונות. נסי שוב בעוד דקה.", status = 429 }, ct);
    };
});

builder.Services.AddProblemDetails();
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

await DbSeeder.InitializeAsync(app.Services, app.Configuration, app.Logger);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
    app.UseHttpsRedirection();
}

// בפרודקשן הלקוח המקומפל נמצא ב-wwwroot ומוגש מאותו שרת.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// לשירות "ping" חיצוני שמונע מהאתר להירדם באחסון חינמי, כדי שהתזכורות והסיכום החודשי ירוצו.
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
