using System.Data;
using System.Globalization;
using ClosedXML.Excel;

namespace AIT_App.Services;

/// <summary>
/// Экспорт таблиц в Excel (ClosedXML).
/// </summary>
public static class ExportService
{
    // ДИЗАЙНЕР: единый стиль заголовков — жирный синий текст; строки — чередование фона и границы.
    private static void ApplyHeader(IXLRange header)
    {
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.DodgerBlue;
        header.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        header.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static void ApplyBodyRow(IXLRange row, bool alternate)
    {
        if (alternate)
            row.Style.Fill.BackgroundColor = XLColor.AliceBlue;
        row.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        row.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    public static void ExportDataTable(DataTable table, string filePath, string sheetName)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(string.IsNullOrWhiteSpace(sheetName) ? "Лист1" : sheetName[..Math.Min(sheetName.Length, 31)]);
        WriteDataTable(ws, table);
        ws.Columns().AdjustToContents();
        wb.SaveAs(filePath);
    }

    private static void WriteDataTable(IXLWorksheet ws, DataTable table)
    {
        var colCount = table.Columns.Count;
        var rowCount = table.Rows.Count;

        for (var c = 0; c < colCount; c++)
            ws.Cell(1, c + 1).Value = table.Columns[c].ColumnName;

        ApplyHeader(ws.Range(1, 1, 1, colCount));

        for (var r = 0; r < rowCount; r++)
        {
            for (var c = 0; c < colCount; c++)
            {
                var v = table.Rows[r][c];
                var cell = ws.Cell(r + 2, c + 1);
                if (v == DBNull.Value || v is null)
                    cell.SetValue(string.Empty);
                else if (v is DateTime dt)
                    cell.SetValue(dt);
                else if (v is bool b)
                    cell.SetValue(b);
                else if (v is IFormattable fmt && v is not string)
                    cell.SetValue(fmt.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty);
                else
                    cell.SetValue(Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty);
            }

            ApplyBodyRow(ws.Range(r + 2, 1, r + 2, colCount), r % 2 == 1);
        }
    }

    /// <summary>
    /// Экспорт отчёта по сессии: лист «Оценки» и лист «Успеваемость».
    /// </summary>
    public static void ExportSessionReport(
        DataTable gradesTable,
        DataTable perfTable,
        string filePath,
        string group,
        DateTime dateFrom,
        DateTime dateTo)
    {
        using var wb = new XLWorkbook();
        var ws1 = wb.AddWorksheet("Оценки");
        WriteDataTable(ws1, gradesTable);
        ws1.SheetView.FreezeRows(1);
        ws1.Columns().AdjustToContents();

        var ws2 = wb.AddWorksheet("Успеваемость");
        WriteDataTable(ws2, perfTable);
        ws2.SheetView.FreezeRows(1);
        ws2.Columns().AdjustToContents();

        wb.Properties.Title = $"Отчёт по сессии — {group}";
        wb.Properties.Subject = $"{dateFrom:yyyy-MM-dd} — {dateTo:yyyy-MM-dd}";
        wb.SaveAs(filePath);
    }

    /// <summary>
    /// Экспорт журнала оценок (pivot-таблица).
    /// </summary>
    public static void ExportJournal(DataTable journalTable, string filePath, string group, string subject)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Журнал");
        WriteDataTable(ws, journalTable);
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
        wb.Properties.Title = $"Журнал — {group} / {subject}";
        wb.SaveAs(filePath);
    }

    /// <summary>
    /// Экспорт блока «Отчёты»: один файл, несколько листов.
    /// </summary>
    public static void ExportReports(
        IReadOnlyDictionary<string, DataTable> sheets,
        string filePath)
    {
        using var wb = new XLWorkbook();
        foreach (var (name, table) in sheets)
        {
            var safe = string.IsNullOrWhiteSpace(name) ? "Лист" : name[..Math.Min(name.Length, 31)];
            var ws = wb.Worksheets.Add(safe);
            WriteDataTable(ws, table);
            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents();
        }

        wb.SaveAs(filePath);
    }
}
