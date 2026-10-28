using ClosedXML.Excel;
using TeamManager.Api.Dtos;

namespace TeamManager.Api.Services;

/// <summary>קובץ אקסל של הסיכום החודשי: גיליון מימין לשמאל, שורה לכל עובדת ושורת סיכום לצוות.</summary>
public static class MonthlySummaryExcel
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static string FileName(int year, int month) => $"monthly-summary-{year}-{month:00}.xlsx";

    private static readonly string[] Headers =
    [
        "שם", "ימי נוכחות", "מתוכם ימים חלקיים", "חופשה", "מחלה", "אחר",
        "ימים חלש", "ימים טוב", "ימים מעולה", "ימי נוכחות בלי ביצוע", "ממוצע עמידה ביעד (%)",
    ];

    public static byte[] Build(MonthlySummaryDto s)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("סיכום");
        ws.RightToLeft = true;

        ws.Cell(1, 1).Value = MonthlySummaryRules.Subject(s.Year, s.Month);
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        const int headerRow = 3;
        for (var c = 0; c < Headers.Length; c++) ws.Cell(headerRow, c + 1).Value = Headers[c];
        var header = ws.Range(headerRow, 1, headerRow, Headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#DDE5EE");

        var row = headerRow + 1;
        foreach (var r in s.Rows)
        {
            ws.Cell(row, 1).Value = r.IsActive ? r.EmployeeName : $"{r.EmployeeName} (לא פעילה)";
            ws.Cell(row, 2).Value = r.PresentDays;
            ws.Cell(row, 3).Value = r.PartialDays;
            ws.Cell(row, 4).Value = r.VacationDays;
            ws.Cell(row, 5).Value = r.SickDays;
            ws.Cell(row, 6).Value = r.OtherDays;
            ws.Cell(row, 7).Value = r.WeakDays;
            ws.Cell(row, 8).Value = r.GoodDays;
            ws.Cell(row, 9).Value = r.ExcellentDays;
            ws.Cell(row, 10).Value = r.DaysWithoutOutput;
            if (r.AveragePercent is { } avg) ws.Cell(row, 11).Value = avg;
            row++;
        }

        // שורת צוות: סכומים, וממוצע על כל ימי הביצוע של הצוות.
        ws.Cell(row, 1).Value = "סה״כ צוות";
        for (var c = 2; c <= 10; c++)
            ws.Cell(row, c).FormulaA1 = $"SUM({ws.Cell(headerRow + 1, c).Address}:{ws.Cell(row - 1, c).Address})";
        if (s.TeamAveragePercent is { } team) ws.Cell(row, 11).Value = team;
        ws.Range(row, 1, row, Headers.Length).Style.Font.Bold = true;
        ws.Range(row, 1, row, Headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;

        ws.Column(11).Style.NumberFormat.Format = "0.00";
        ws.Columns(1, Headers.Length).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
