using ClosedXML.Excel;
using TeamManager.Api.Dtos;

namespace TeamManager.Api.Services;

/// <summary>
/// קובץ אקסל של הסיכום החודשי: גיליון מימין לשמאל, שורה לכל עובדת ושורת סיכום לצוות.
/// אחרי עמודות הנוכחות יש עמודה לכל יום בחודש עם מספר הפריטים שבוצעו, ובסוף סה״כ חודשי.
/// </summary>
public static class MonthlySummaryExcel
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static string FileName(int year, int month) => $"monthly-summary-{year}-{month:00}.xlsx";

    private static readonly string[] Headers =
    [
        "שם", "ימי נוכחות", "מתוכם ימים חלקיים", "חופשה", "מחלה", "אחר",
        "ימי נוכחות בלי ביצוע", "ממוצע עמידה ביעד (%)",
    ];

    private const int AverageColumn = 8;
    private const int FirstDayColumn = AverageColumn + 1;

    public static byte[] Build(MonthlySummaryDto s, IReadOnlyDictionary<int, Dictionary<int, int>> dailyOutput)
    {
        var days = DateTime.DaysInMonth(s.Year, s.Month);
        var totalColumn = FirstDayColumn + days;

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("סיכום");
        ws.RightToLeft = true;

        ws.Cell(1, 1).Value = MonthlySummaryRules.Subject(s.Year, s.Month);
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        const int headerRow = 3;
        for (var c = 0; c < Headers.Length; c++) ws.Cell(headerRow, c + 1).Value = Headers[c];
        for (var d = 1; d <= days; d++)
        {
            var column = FirstDayColumn + d - 1;
            ws.Cell(headerRow, column).Value = $"{d}/{s.Month}";
            // שישי, שבת וחגים ברקע אפור, כדי שיהיה קל להתמצא בשבועות.
            if (!HolidayCalendar.IsWorkday(new DateOnly(s.Year, s.Month, d)))
                ws.Column(column).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
        }
        ws.Cell(headerRow, totalColumn).Value = "סה״כ הספק";

        var header = ws.Range(headerRow, 1, headerRow, totalColumn);
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
            ws.Cell(row, 7).Value = r.DaysWithoutOutput;
            if (r.AveragePercent is { } avg) ws.Cell(row, AverageColumn).Value = avg;

            if (dailyOutput.TryGetValue(r.EmployeeId, out var byDay))
                foreach (var (day, completed) in byDay)
                    ws.Cell(row, FirstDayColumn + day - 1).Value = completed;
            ws.Cell(row, totalColumn).FormulaA1 =
                $"SUM({ws.Cell(row, FirstDayColumn).Address}:{ws.Cell(row, totalColumn - 1).Address})";
            row++;
        }

        // שורת צוות: סכומים, וממוצע על כל ימי הביצוע של הצוות.
        ws.Cell(row, 1).Value = "סה״כ צוות";
        foreach (var c in Enumerable.Range(2, AverageColumn - 2).Concat(Enumerable.Range(FirstDayColumn, days + 1)))
            ws.Cell(row, c).FormulaA1 = $"SUM({ws.Cell(headerRow + 1, c).Address}:{ws.Cell(row - 1, c).Address})";
        if (s.TeamAveragePercent is { } team) ws.Cell(row, AverageColumn).Value = team;
        ws.Range(row, 1, row, totalColumn).Style.Font.Bold = true;
        ws.Range(row, 1, row, totalColumn).Style.Border.TopBorder = XLBorderStyleValues.Thin;

        ws.Column(AverageColumn).Style.NumberFormat.Format = "0.00";
        ws.Columns(FirstDayColumn, totalColumn).Style.NumberFormat.Format = "#,##0";
        ws.Columns(1, totalColumn).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
