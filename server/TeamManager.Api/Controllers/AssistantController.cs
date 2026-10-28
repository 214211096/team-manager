using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TeamManager.Api.Services;

namespace TeamManager.Api.Controllers;

public record AssistantRequest([Required, MinLength(1), MaxLength(40)] List<AssistantTurn> Messages);

/// <summary>עוזר AI לשאלות על נתוני הצוות. קריאה בלבד, לשתי המשתמשות.</summary>
[ApiController]
[Route("api/assistant")]
[Authorize]
public class AssistantController(AssistantService assistant) : ControllerBase
{
    /// <summary>מגבלת שאלות לעוזר (מוגדרת ב-Program), כי כל שאלה עולה כסף.</summary>
    public const string RateLimitPolicy = "assistant";

    [HttpGet("status")]
    public object Status() => new { enabled = assistant.Enabled };

    [HttpPost("chat")]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<ActionResult<object>> Chat(AssistantRequest req, CancellationToken ct)
    {
        var last = req.Messages[^1];
        if (last.Role != "user" || string.IsNullOrWhiteSpace(last.Content))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "ההודעה האחרונה צריכה להיות שאלה");
        if (req.Messages.Any(m => m.Role is not ("user" or "assistant") || m.Content.Length > 4000))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "הודעה לא תקינה או ארוכה מדי");

        var reply = await assistant.AskAsync(req.Messages, ct);
        if (reply.Error is not null)
            return Problem(statusCode: assistant.Enabled ? StatusCodes.Status502BadGateway : StatusCodes.Status503ServiceUnavailable,
                title: reply.Error);
        return new { answer = reply.Answer };
    }
}
