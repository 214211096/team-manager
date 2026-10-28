using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using TeamManager.Api.Data;
using TeamManager.Api.Entities;

namespace TeamManager.Api.Services;

/// <summary>הגדרות שליחת מייל (סעיף Email בקונפיגורציה).</summary>
public class EmailOptions
{
    public string From { get; set; } = "";
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 25;
    public bool EnableSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }

    /// <summary>לפיתוח: במקום לשלוח, כל מייל נשמר כקובץ .eml בתיקייה זו (יחסית לתיקיית האפליקציה).</summary>
    public string? PickupDirectory { get; set; }
}

public record EmailAttachment(string FileName, byte[] Content, string ContentType);

public record EmailMessage(
    string Type,
    IReadOnlyList<string> To,
    string Subject,
    string HtmlBody,
    DateOnly? ForDate = null,
    IReadOnlyList<EmailAttachment>? Attachments = null);

public interface IEmailSender
{
    /// <summary>שולח ורושם ב-EmailLog. לא זורק על כשל שליחה: מחזיר את השגיאה, או null בהצלחה.</summary>
    Task<string?> SendAsync(EmailMessage message, CancellationToken ct = default);
}

public class SmtpEmailSender(
    AppDbContext db,
    IOptions<EmailOptions> options,
    IHostEnvironment env,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<string?> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        string? error = null;
        try
        {
            await DeliverAsync(message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Sending {Type} email failed", message.Type);
            error = ex.Message;
        }

        var to = string.Join(", ", message.To);
        db.EmailLogs.Add(new EmailLog
        {
            Type = message.Type,
            To = to.Length > 200 ? to[..200] : to,
            Success = error is null,
            Error = error,
            ForDate = message.ForDate,
        });
        await db.SaveChangesAsync(ct);
        return error;
    }

    private async Task DeliverAsync(EmailMessage message, CancellationToken ct)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.From))
            throw new InvalidOperationException("Email:From is not configured.");

        var mail = new MimeMessage { Subject = message.Subject };
        mail.From.Add(MailboxAddress.Parse(o.From));
        foreach (var address in message.To) mail.To.Add(MailboxAddress.Parse(address));
        var body = new BodyBuilder { HtmlBody = message.HtmlBody };
        foreach (var a in message.Attachments ?? [])
            body.Attachments.Add(a.FileName, a.Content, ContentType.Parse(a.ContentType));
        mail.Body = body.ToMessageBody();

        if (!string.IsNullOrWhiteSpace(o.PickupDirectory))
        {
            var dir = Path.Combine(env.ContentRootPath, o.PickupDirectory);
            Directory.CreateDirectory(dir);
            await mail.WriteToAsync(Path.Combine(dir, $"{DateTime.Now:yyyyMMdd-HHmmss}-{message.Type}-{Guid.NewGuid():N}.eml"), ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(o.SmtpHost))
            throw new InvalidOperationException("No SMTP server is configured (Email:SmtpHost).");

        using var client = new SmtpClient();
        var security = o.EnableSsl ? SecureSocketOptions.Auto : SecureSocketOptions.None;
        await client.ConnectAsync(o.SmtpHost, o.SmtpPort, security, ct);
        if (!string.IsNullOrWhiteSpace(o.UserName))
            await client.AuthenticateAsync(o.UserName, o.Password ?? "", ct);
        await client.SendAsync(mail, ct);
        await client.DisconnectAsync(true, ct);
    }
}
