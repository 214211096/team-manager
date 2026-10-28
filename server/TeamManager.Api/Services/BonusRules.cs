using System.Globalization;
using System.Net;
using System.Text;
using ClosedXML.Excel;
using TeamManager.Api.Dtos;

namespace TeamManager.Api.Services;

/// <summary>כללי הבונוס, תוכן המייל לשכר וקובץ האקסל, בלי תלות במסד נתונים.</summary>
public static class BonusRules
{
    public const int NoteMaxLength = 500;

    public static Dictionary<string, string[]> Validate(IReadOnlyList<BonusEntryRequest> entries, decimal cap)
    {
        var errors = new Dictionary<string, string[]>();
        if (entries.Select(e => e.EmployeeId).Distinct().Count() != entries.Count)
            errors["entries"] = ["עובדת מופיעה יותר מפעם אחת"];

        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.Amount is { } a && (a < 0 || a > cap))
                errors[$"entries[{i}].amount"] = [$"סכום בונוס בין 0 ל-{cap:0.##} ש״ח"];
            else if (e.Amount is { } b && decimal.Round(b, 2) != b)
                errors[$"entries[{i}].amount"] = ["עד שתי ספרות אחרי הנקודה"];
            if (e.Note?.Trim().Length > NoteMaxLength)
                errors[$"entries[{i}].note"] = [$"הערה עד {NoteMaxLength} תווים"];
        }
        return errors;
    }

    /// <summary>אפשר לשלוח לשכר רק חודש שהסתיים, כדי לא לנעול נוכחות של ימים שעוד לא עברו.</summary>
    public static bool MonthEnded(int year, int month, DateOnly today) => new DateOnly(year, month, 1).AddMonths(1) <= today;

    public static string Shekel(decimal amount) => amount.ToString("#,0.##", CultureInfo.InvariantCulture) + " ש״ח";

    public static string Subject(int year, int month) => $"בונוסים לשכר – {MonthlySummaryRules.MonthName(year, month)}";

    public static string Html(BonusMonthDto b, string lockedBy)
    {
        static string Encode(string v) => WebUtility.HtmlEncode(v);

        var html = new StringBuilder();
        html.Append("<div dir=\"rtl\" style=\"font-family:Arial,sans-serif\">");
        html.Append($"<h2>{Encode(Subject(b.Year, b.Month))}</h2>");
        html.Append("<table border=\"1\" cellpadding=\"6\" style=\"border-collapse:collapse\">");
        html.Append("<tr><th>שם</th><th>סכום</th><th>הערה</th></tr>");
        foreach (var r in b.Rows)
            html.Append($"<tr><td>{Encode(r.Summary.EmployeeName)}</td><td>{Encode(Shekel(r.Amount ?? 0))}</td><td>{Encode(r.Note ?? "")}</td></tr>");
        html.Append($"<tr><td><b>סה״כ</b></td><td><b>{Encode(Shekel(b.Total))}</b></td><td></td></tr>");
        html.Append("</table>");
        html.Append($"<p>נשלח על ידי {Encode(lockedBy)}. החודש ננעל לעריכה במערכת. קובץ אקסל מצורף.</p>");
        html.Append("</div>");
        return html.ToString();
    }

    public static string ExcelFileName(int year, int month) => $"bonuses-{year}-{month:00}.xlsx";

    public static byte[] Excel(BonusMonthDto b)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("בונוסים");
        ws.RightToLeft = true;

        ws.Cell(1, 1).Value = Subject(b.Year, b.Month);
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        string[] headers = ["שם", "סכום (ש״ח)", "הערה"];
        for (var c = 0; c < headers.Length; c++) ws.Cell(3, c + 1).Value = headers[c];
        ws.Range(3, 1, 3, headers.Length).Style.Font.Bold = true;
        ws.Range(3, 1, 3, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#DDE5EE");

        var row = 4;
        foreach (var r in b.Rows)
        {
            ws.Cell(row, 1).Value = r.Summary.EmployeeName;
            ws.Cell(row, 2).Value = r.Amount ?? 0;
            ws.Cell(row, 3).Value = r.Note ?? "";
            row++;
        }
        ws.Cell(row, 1).Value = "סה״כ";
        ws.Cell(row, 2).FormulaA1 = $"SUM(B4:B{row - 1})";
        ws.Range(row, 1, row, headers.Length).Style.Font.Bold = true;
        ws.Range(row, 1, row, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;

        ws.Column(2).Style.NumberFormat.Format = "#,##0.00";
        ws.Columns(1, headers.Length).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
