using ClosedXML.Excel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;

namespace DM_RMS.Wpf.Helpers;

public static class DataTableExportHelper
{
    public static string ToTsv(DataView? view, IEnumerable<string>? hiddenColumns = null)
    {
        if (view == null) return string.Empty;
        return ToTsv(CreateVisibleTable(view, hiddenColumns));
    }

    public static string ToTsv(DataTable? table)
    {
        if (table == null) return string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine(string.Join("\t", table.Columns.Cast<DataColumn>().Select(c => Escape(c.ColumnName))));
        foreach (DataRow row in table.Rows)
        {
            sb.AppendLine(string.Join("\t", table.Columns.Cast<DataColumn>().Select(c => Escape(row[c]))));
        }
        return sb.ToString();
    }

    public static void CopyToClipboard(DataView? view, IEnumerable<string>? hiddenColumns = null)
    {
        var tsv = ToTsv(view, hiddenColumns);
        if (!string.IsNullOrWhiteSpace(tsv)) System.Windows.Clipboard.SetText(tsv);
    }

    public static void CopyToClipboard(DataTable? table)
    {
        var tsv = ToTsv(table);
        if (!string.IsNullOrWhiteSpace(tsv)) System.Windows.Clipboard.SetText(tsv);
    }

    public static void SaveCsv(DataView? view, string path, IEnumerable<string>? hiddenColumns = null)
    {
        if (view == null) return;
        var table = CreateVisibleTable(view, hiddenColumns);
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine(string.Join(",", table.Columns.Cast<DataColumn>().Select(c => Csv(c.ColumnName))));
        foreach (DataRow row in table.Rows)
        {
            writer.WriteLine(string.Join(",", table.Columns.Cast<DataColumn>().Select(c => Csv(row[c]))));
        }
    }

    public static void SaveExcel(DataView? view, string path, string worksheetName = "Data", IEnumerable<string>? hiddenColumns = null)
    {
        if (view == null) return;
        SaveExcel(CreateVisibleTable(view, hiddenColumns), path, worksheetName);
    }

    public static DataTable CreateVisibleTable(DataView view, IEnumerable<string>? hiddenColumns)
    {
        var table = view.ToTable();
        var hidden = (hiddenColumns ?? Array.Empty<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in hidden.ToArray())
            if (table.Columns.Contains(name)) table.Columns.Remove(name);
        return table;
    }

    public static void SaveExcel(DataTable? table, string path, string worksheetName = "Data")
    {
        if (table == null) return;
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(SanitizeWorksheetName(worksheetName));
        worksheet.Cell(1, 1).InsertTable(table, true);
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents(1, Math.Min(table.Rows.Count + 1, 300));
        workbook.SaveAs(path);
    }

    private static string SanitizeWorksheetName(string name)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        foreach (var c in invalid) name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "Data" : name[..Math.Min(name.Length, 31)];
    }

    private static string Escape(object? value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture)?.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ") ?? string.Empty;

    private static string Csv(object? value)
    {
        var text = Escape(value).Replace("\"", "\"\"");
        return $"\"{text}\"";
    }
}
