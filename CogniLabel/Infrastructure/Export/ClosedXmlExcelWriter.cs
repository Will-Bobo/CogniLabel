using ClosedXML.Excel;
using CogniLabel.Application.Dtos;
using CogniLabel.Application.Pipeline;
using CogniLabel.Shared;
using CogniLabel.Shared.Enums;
using System.Globalization;

namespace CogniLabel.Infrastructure.Export;

public sealed class ClosedXmlExcelWriter : IExcelWriter
{
    public void WriteReport(string filePath, AuditResult auditResult, IReadOnlyList<AuditItem> items)
    {
        using var wb = new XLWorkbook();

        WriteSummary(wb, auditResult);
        WriteDetails(wb, items, auditResult);
        WriteErrors(wb, items);
        WriteDuplicates(wb, items);
        WriteUnreadable(wb, items);

        wb.SaveAs(filePath);
    }

    private static void WriteSummary(XLWorkbook wb, AuditResult audit)
    {
        var ws = wb.Worksheets.Add(Strings.Report.SheetSummary);

        ws.Cell(1, 1).Value = Strings.Report.ColTotal;
        ws.Cell(1, 2).Value = audit.Summary.Total;

        ws.Cell(2, 1).Value = Strings.Report.ColPass;
        ws.Cell(2, 2).Value = audit.Summary.Pass;

        ws.Cell(3, 1).Value = Strings.Report.ColFail;
        ws.Cell(3, 2).Value = audit.Summary.Fail;

        ws.Cell(4, 1).Value = Strings.Report.ColRunTime;
        ws.Cell(4, 2).Value = audit.Meta.StartTime.ToString("u", CultureInfo.InvariantCulture);

        // Inputs are not stored in AuditResult today; keep the cells but leave empty.
        ws.Cell(5, 1).Value = Strings.Report.ColExcelPath;
        ws.Cell(5, 2).Value = string.Empty;
        ws.Cell(6, 1).Value = Strings.Report.ColImageFolder;
        ws.Cell(6, 2).Value = string.Empty;
        ws.Cell(7, 1).Value = Strings.Report.ColTemplatePath;
        ws.Cell(7, 2).Value = string.Empty;

        ws.Columns().AdjustToContents();
    }

    private static void WriteDetails(XLWorkbook wb, IReadOnlyList<AuditItem> items, AuditResult audit)
    {
        var ws = wb.Worksheets.Add(Strings.Report.SheetDetails);

        // Header
        var col = 1;
        ws.Cell(1, col++).Value = Strings.Report.ColImageName;
        ws.Cell(1, col++).Value = Strings.Report.ColSn;
        ws.Cell(1, col++).Value = Strings.Report.ColMatchStatus;
        ws.Cell(1, col++).Value = Strings.Report.ColErrorType;

        // Expand mapped fields: <Field>_Image, <Field>_Excel
        var mappings = GuessMappingsFromItems(items);
        var expanded = mappings.Keys.OrderBy(x => x, StringComparer.Ordinal).ToList();

        foreach (var f in expanded)
        {
            ws.Cell(1, col++).Value = $"{f}_Image";
            ws.Cell(1, col++).Value = $"{f}_Excel";
        }

        for (var i = 0; i < items.Count; i++)
        {
            var row = i + 2;
            var item = items[i];

            var sn = item.Image.Fields.TryGetValue("SN", out var snValue) ? snValue : null;
            ws.Cell(row, 1).Value = item.Image.ImageName;
            ws.Cell(row, 2).Value = sn ?? string.Empty;
            ws.Cell(row, 3).Value = item.IsPass ? "PASS" : "FAIL";
            ws.Cell(row, 4).Value = ErrorTypeDisplay.GetErrorDisplay(item.ErrorType);

            col = 5;
            foreach (var f in expanded)
            {
                item.Image.Fields.TryGetValue(f, out var imgV);
                var excelV = item.ExcelValues is null ? null : GetExcelValue(item.ExcelValues, mappings[f]);
                ws.Cell(row, col++).Value = imgV ?? string.Empty;
                ws.Cell(row, col++).Value = excelV ?? string.Empty;
            }
        }

        ws.Columns().AdjustToContents();
    }

    private static void WriteErrors(XLWorkbook wb, IReadOnlyList<AuditItem> items)
    {
        var ws = wb.Worksheets.Add(Strings.Report.SheetErrors);

        ws.Cell(1, 1).Value = Strings.Report.ColImageName;
        ws.Cell(1, 2).Value = Strings.Report.ColFieldName;
        ws.Cell(1, 3).Value = Strings.Report.ColErrorType;
        ws.Cell(1, 4).Value = Strings.Report.ColImageValue;
        ws.Cell(1, 5).Value = Strings.Report.ColExcelValue;

        var row = 2;
        foreach (var item in items)
        {
            foreach (var issue in item.FieldIssues)
            {
                ws.Cell(row, 1).Value = item.Image.ImageName;
                ws.Cell(row, 2).Value = issue.FieldName;
                ws.Cell(row, 3).Value = ErrorTypeDisplay.GetErrorDisplay(issue.ErrorType);

                item.Image.Fields.TryGetValue(issue.FieldName, out var imgV);
                ws.Cell(row, 4).Value = imgV ?? string.Empty;

                var excelV = item.ExcelValues is null ? null : GetExcelValue(item.ExcelValues, issue.FieldName);
                ws.Cell(row, 5).Value = excelV ?? string.Empty;
                row++;
            }
        }

        ws.Columns().AdjustToContents();
    }

    private static void WriteDuplicates(XLWorkbook wb, IReadOnlyList<AuditItem> items)
    {
        var ws = wb.Worksheets.Add(Strings.Report.SheetDuplicates);

        ws.Cell(1, 1).Value = Strings.Report.ColSn;
        ws.Cell(1, 2).Value = Strings.Report.ColCount;
        ws.Cell(1, 3).Value = Strings.Report.ColImages;

        var duplicates = items
            .Select(i => i.Image.Fields.TryGetValue("SN", out var sn) ? sn : null)
            .Where(sn => !string.IsNullOrWhiteSpace(sn))
            .GroupBy(sn => sn!, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        var row = 2;
        foreach (var g in duplicates)
        {
            var sn = g.Key;
            var imgs = items
                .Where(i => i.Image.Fields.TryGetValue("SN", out var s) && string.Equals(s, sn, StringComparison.Ordinal))
                .Select(i => i.Image.ImageName)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            ws.Cell(row, 1).Value = sn;
            ws.Cell(row, 2).Value = imgs.Count;
            ws.Cell(row, 3).Value = string.Join(",", imgs);
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void WriteUnreadable(XLWorkbook wb, IReadOnlyList<AuditItem> items)
    {
        var ws = wb.Worksheets.Add(Strings.Report.SheetUnreadable);

        ws.Cell(1, 1).Value = Strings.Report.ColImageName;
        ws.Cell(1, 2).Value = Strings.Report.ColReason;

        var row = 2;
        foreach (var item in items.Where(i => i.ErrorType == ErrorType.Unreadable))
        {
            ws.Cell(row, 1).Value = item.Image.ImageName;
            ws.Cell(row, 2).Value = ErrorTypeDisplay.GetErrorDisplay(ErrorType.Unreadable);
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private static IReadOnlyDictionary<string, string> GuessMappingsFromItems(IReadOnlyList<AuditItem> items)
    {
        // Best-effort: map field name to itself (Excel column name unknown at export time)
        // This keeps export stable while respecting "export from AuditResult only".
        var fields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var it in items)
        {
            foreach (var k in it.Image.Fields.Keys)
                fields.Add(k);
        }

        return fields.ToDictionary(k => k, v => v, StringComparer.Ordinal);
    }

    private static string? GetExcelValue(IReadOnlyDictionary<string, string> excelRow, string excelColumn)
        => excelRow.TryGetValue(excelColumn, out var v) ? v : null;
}

