using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Options;

namespace TeamManager.Api.Services;

/// <summary>הגדרות העוזר (סעיף Assistant בקונפיגורציה). בלי מפתח העוזר כבוי.</summary>
public class AssistantOptions
{
    public string? ApiKey { get; set; }
    /// <summary>ברירת המחדל היא המודל הזול ביותר; שאלות על נתונים לא צריכות יותר.</summary>
    public string Model { get; set; } = "claude-haiku-5-5";
}

public record AssistantTurn(string Role, string Content);

public record AssistantReply(string? Answer, string? Error);

/// <summary>
/// עוזר AI שעונה על שאלות על נתוני הצוות. Claude מפעיל את הכלים (קריאה בלבד) עד שיש לו תשובה.
/// השיחה נשלחת מהלקוח כטקסט בכל בקשה; בתוך בקשה ההיסטוריה רק מתווספת (בלי עריכה), כנדרש ל-thinking.
/// </summary>
public class AssistantService(
    IOptions<AssistantOptions> options,
    AssistantTools tools,
    TimeProvider clock,
    ILogger<AssistantService> logger)
{
    /// <summary>הגנה מלולאה אינסופית של קריאות לכלים בשאלה אחת.</summary>
    private const int MaxToolRounds = 10;
    private const int MaxHistoryTurns = 20;

    public bool Enabled => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    private static readonly List<BetaToolUnion> ToolDefinitions = AssistantTools.Definitions
        .Select(d => (BetaToolUnion)new BetaTool
        {
            Name = d.Name,
            Description = d.Description,
            InputSchema = new()
            {
                Properties = d.Properties.ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value)),
                Required = d.Required,
            },
        })
        .ToList();

    public async Task<AssistantReply> AskAsync(IReadOnlyList<AssistantTurn> history, CancellationToken ct)
    {
        if (!Enabled) return new(null, "העוזר עוד לא הוגדר: חסר מפתח API של Anthropic בהגדרות השרת.");

        var client = new AnthropicClient { ApiKey = options.Value.ApiKey };
        List<BetaMessageParam> messages = history
            .TakeLast(MaxHistoryTurns)
            .Select(t => t.Role == "assistant"
                ? (BetaMessageParam)new BetaAssistantMessageParam { Content = t.Content }
                : new BetaUserMessageParam { Content = t.Content })
            .ToList();

        try
        {
            for (var round = 0; round < MaxToolRounds; round++)
            {
                var response = await client.Beta.Messages.Create(new MessageCreateParams
                {
                    Model = options.Value.Model,
                    MaxTokens = 16000,
                    System = SystemPrompt(),
                    Messages = messages,
                    Tools = ToolDefinitions,
                    // מאמץ נמוך: פחות טוקני חשיבה ופחות סבבי כלים, כלומר זול יותר. מספיק לשאלות על נתונים.
                    OutputConfig = new BetaOutputConfig { Effort = Effort.Low },
                }, ct);

                if (response.StopReason == "refusal")
                    return new(null, "העוזר לא יכול לענות על השאלה הזאת. נסי לנסח אותה אחרת.");

                var (assistantContent, toolUses) = Echo(response.Content);
                var text = string.Join("\n", response.Content
                    .Select(b => b.TryPickText(out var t) ? t.Text : null)
                    .Where(t => !string.IsNullOrWhiteSpace(t)));

                if (toolUses.Count == 0 || response.StopReason != "tool_use")
                {
                    if (response.StopReason == "max_tokens" && text.Length == 0)
                        return new(null, "התשובה ארוכה מדי. נסי לשאול שאלה ממוקדת יותר.");
                    return new(text.Length > 0 ? text : "לא מצאתי תשובה.", null);
                }

                // כל הכלים של סבב אחד מוחזרים בהודעה אחת, תוצאה לכל tool_use.
                List<BetaContentBlockParam> results = [];
                foreach (var use in toolUses)
                {
                    var output = await tools.ExecuteAsync(use.Name, use.Input);
                    results.Add(new BetaToolResultBlockParam { ToolUseID = use.ID, Content = output });
                }
                messages.Add(new BetaAssistantMessageParam { Content = assistantContent });
                messages.Add(new BetaUserMessageParam { Content = results });
            }

            return new(null, "השאלה דרשה יותר מדי חיפושים. נסי לשאול על טווח תאריכים קטן יותר.");
        }
        catch (AnthropicRateLimitException ex)
        {
            logger.LogWarning(ex, "Assistant rate limited");
            return new(null, "העוזר עמוס כרגע. נסי שוב בעוד דקה.");
        }
        catch (AnthropicApiException ex)
        {
            logger.LogError(ex, "Assistant API call failed");
            return new(null, "העוזר לא זמין כרגע. נסי שוב מאוחר יותר.");
        }
    }

    /// <summary>
    /// ממיר את תוכן התשובה לפרמטרים לשליחה חוזרת. אחרי מעבר למודל חלופי (בלוק fallback),
    /// לא מחזירים thinking ו-tool_use שלפני המעבר: הם של המודל שסירב, ולא מריצים את הכלים שלו.
    /// </summary>
    private static (List<BetaContentBlockParam> Content, List<BetaToolUseBlock> ToolUses) Echo(IReadOnlyList<BetaContentBlock> blocks)
    {
        var lastFallback = -1;
        for (var i = 0; i < blocks.Count; i++)
            if (blocks[i].TryPickFallback(out _)) lastFallback = i;

        List<BetaContentBlockParam> content = [];
        List<BetaToolUseBlock> toolUses = [];
        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var beforeFallback = i < lastFallback;
            if (block.TryPickText(out var text))
            {
                content.Add(new BetaTextBlockParam { Text = text.Text });
            }
            else if (beforeFallback)
            {
                continue;
            }
            else if (block.TryPickThinking(out var thinking))
            {
                content.Add(new BetaThinkingBlockParam { Thinking = thinking.Thinking, Signature = thinking.Signature });
            }
            else if (block.TryPickRedactedThinking(out var redacted))
            {
                content.Add(new BetaRedactedThinkingBlockParam { Data = redacted.Data });
            }
            else if (block.TryPickToolUse(out var use))
            {
                content.Add(new BetaToolUseBlockParam { ID = use.ID, Name = use.Name, Input = use.Input });
                toolUses.Add(use);
            }
        }
        return (content, toolUses);
    }

    private string SystemPrompt()
    {
        var today = clock.GetLocalNow();
        return $"""
            את עוזרת של מנהלת צוות במערכת פנימית לניהול נוכחות, ביצוע יומי ובונוסים.
            עני על שאלות על נתוני הצוות בעברית, בקצרה ולעניין.

            היום {AttendanceRules.FormatDate(DateOnly.FromDateTime(today.DateTime))} (yyyy-MM-dd: {today:yyyy-MM-dd}).
            "החודש" ו"אתמול" הם ביחס לתאריך הזה. ימי עבודה: ראשון עד חמישי.

            כללים:
            - עני רק לפי מה שהכלים מחזירים. אל תנחשי מספרים, תאריכים או שמות.
            - אם שם לא מזוהה בוודאות, בדקי ב-list_employees. אם יש כמה התאמות, שאלי למי הכוונה.
            - אם אין נתונים (למשל חודש לפני שהמערכת התחילה לפעול, או יום שלא דווח), אמרי זאת במפורש.
            - יום בלי רשומת נוכחות פירושו שלא דווח, לא שהעובדת נכחה.
            - בממוצעים: ימי היעדרות וימים בלי ביצוע לא נספרים.
            - תאריכים בפורמט dd/mm/yyyy, סכומים בש"ח.
            - טקסט פשוט בלי Markdown (בלי כוכביות, כותרות או טבלאות). לרשימה: שורה לכל פריט שמתחילה ב"- ".
            - אין לך אפשרות לשנות נתונים. אם מבקשים שינוי, הפני למסך המתאים במערכת.
            - סיבות היעדרות הן כלליות; אל תוסיפי השערות על מצב רפואי או אישי.
            """;
    }
}
