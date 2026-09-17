using ClosedXML.Excel;

namespace MoeezMobile.Api.Services;

/// <summary>Column definition: the header shown, and how to pull the value off a row.</summary>
public record ExcelColumn<T>(string Header, Func<T, object?> Value, string? NumberFormat = null);

public interface IExcelExportService
{
    /// <summary>Renders rows to an .xlsx byte array with an optional summary strip on top.</summary>
    byte[] Build<T>(
        string sheetTitle,
        IEnumerable<T> rows,
        IReadOnlyList<ExcelColumn<T>> columns,
        IReadOnlyDictionary<string, decimal>? summary = null,
        string? subtitle = null);
}

public class ExcelExportService : IExcelExportService
{
    private const string MoneyFormat = "#,##0.00";

    public byte[] Build<T>(
        string sheetTitle,
        IEnumerable<T> rows,
        IReadOnlyList<ExcelColumn<T>> columns,
        IReadOnlyDictionary<string, decimal>? summary = null,
        string? subtitle = null)
    {
        using var workbook = new XLWorkbook();
        // Excel sheet names are limited to 31 chars and cannot contain : \ / ? * [ ]
        var sheet = workbook.Worksheets.Add(SafeSheetName(sheetTitle));
        sheet.RightToLeft = true;

        var row = 1;

        sheet.Cell(row, 1).Value = sheetTitle;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Font.FontSize = 14;
        sheet.Range(row, 1, row, Math.Max(1, columns.Count)).Merge();
        row++;

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            sheet.Cell(row, 1).Value = subtitle;
            sheet.Range(row, 1, row, Math.Max(1, columns.Count)).Merge();
            row++;
        }

        if (summary is { Count: > 0 })
        {
            row++;
            foreach (var kv in summary)
            {
                sheet.Cell(row, 1).Value = kv.Key;
                sheet.Cell(row, 1).Style.Font.Bold = true;
                sheet.Cell(row, 2).Value = kv.Value;
                sheet.Cell(row, 2).Style.NumberFormat.Format = MoneyFormat;
                row++;
            }
        }

        row++;
        var headerRow = row;
        for (var c = 0; c < columns.Count; c++)
        {
            var cell = sheet.Cell(headerRow, c + 1);
            cell.Value = columns[c].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }
        row++;

        foreach (var item in rows)
        {
            for (var c = 0; c < columns.Count; c++)
            {
                var cell = sheet.Cell(row, c + 1);
                SetValue(cell, columns[c].Value(item));
                if (columns[c].NumberFormat is { } fmt) cell.Style.NumberFormat.Format = fmt;
            }
            row++;
        }

        if (columns.Count > 0)
            sheet.Range(headerRow, 1, Math.Max(headerRow, row - 1), columns.Count).SetAutoFilter();

        sheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void SetValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null: cell.Value = string.Empty; break;
            case string s: cell.Value = s; break;
            case DateTime d: cell.Value = d; cell.Style.NumberFormat.Format = "yyyy-mm-dd"; break;
            case decimal m: cell.Value = m; break;
            case double db: cell.Value = db; break;
            case int i: cell.Value = i; break;
            case long l: cell.Value = l; break;
            case bool b: cell.Value = b ? "Yes" : "No"; break;
            default: cell.Value = value.ToString(); break;
        }
    }

    private static string SafeSheetName(string name)
    {
        var cleaned = new string(name.Where(ch => !":\\/?*[]".Contains(ch)).ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = "Report";
        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }

    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
