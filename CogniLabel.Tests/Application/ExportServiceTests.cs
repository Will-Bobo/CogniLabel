using CogniLabel.Application.Dtos;
using CogniLabel.Application.Export;
using CogniLabel.Application.Pipeline;
using CogniLabel.Application.SingleImage;
using CogniLabel.Infrastructure.Export;
using CogniLabel.Infrastructure.IO;
using CogniLabel.Shared;
using CogniLabel.Shared.Enums;
using ClosedXML.Excel;
using System.IO;

namespace CogniLabel.Tests.Application;

public sealed class ExportServiceTests
{
    [Fact]
    public void Report_generation_should_create_excel_with_expected_sheets_and_row_counts()
    {
        var tmp = MakeTempDir();
        var audit = BuildAuditResult(tmp, items: new[]
        {
            MakeItem(tmp, "a.png", sn: "A", isPass: true, error: ErrorType.None),
            MakeItem(tmp, "b.png", sn: "B", isPass: false, error: ErrorType.NotFound),
            MakeItem(tmp, "c.png", sn: "B", isPass: false, error: ErrorType.Duplicate),
        });

        var clock = new FakeClock(new DateTimeOffset(2026, 4, 25, 10, 0, 0, TimeSpan.Zero));
        var sut = new ExportService(new ClosedXmlExcelWriter(), new FileSystemService(), clock);

        var result = sut.Export(audit, outputRoot: tmp);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OutputPath);
        var reportPath = Path.Combine(result.OutputPath!, Strings.Export.ReportFileName);
        Assert.True(File.Exists(reportPath));

        using var wb = new XLWorkbook(reportPath);
        Assert.NotNull(wb.Worksheet(Strings.Report.SheetSummary));
        Assert.NotNull(wb.Worksheet(Strings.Report.SheetDetails));
        Assert.NotNull(wb.Worksheet(Strings.Report.SheetErrors));
        Assert.NotNull(wb.Worksheet(Strings.Report.SheetDuplicates));
        Assert.NotNull(wb.Worksheet(Strings.Report.SheetUnreadable));

        var details = wb.Worksheet(Strings.Report.SheetDetails);
        Assert.Equal(4, details.LastRowUsed()!.RowNumber()); // header + 3 rows
    }

    [Fact]
    public void Error_image_classification_should_copy_only_failed_images_to_expected_folders()
    {
        var tmp = MakeTempDir();

        var audit = BuildAuditResult(tmp, items: new[]
        {
            MakeItem(tmp, "pass.png", sn: "A", isPass: true, error: ErrorType.None),
            MakeItem(tmp, "nf.png", sn: "B", isPass: false, error: ErrorType.NotFound),
            MakeItem(tmp, "mm.png", sn: "C", isPass: false, error: ErrorType.Mismatch),
            MakeItem(tmp, "ur.png", sn: null, isPass: false, error: ErrorType.Unreadable),
            MakeItem(tmp, "du.png", sn: "D", isPass: false, error: ErrorType.Duplicate),
        });

        var clock = new FakeClock(new DateTimeOffset(2026, 4, 25, 10, 0, 1, TimeSpan.Zero));
        var sut = new ExportService(new ClosedXmlExcelWriter(), new FileSystemService(), clock);
        var result = sut.Export(audit, outputRoot: tmp);

        Assert.True(result.IsSuccess);
        var outDir = result.OutputPath!;

        Assert.False(File.Exists(Path.Combine(outDir, Strings.Export.ImagesFolder, "pass.png")));

        Assert.True(File.Exists(Path.Combine(outDir, Strings.Export.ImagesFolder, Strings.Export.ErrorFolder, Strings.Export.NotFoundFolder, "nf.png")));
        Assert.True(File.Exists(Path.Combine(outDir, Strings.Export.ImagesFolder, Strings.Export.ErrorFolder, Strings.Export.MismatchFolder, "mm.png")));
        Assert.True(File.Exists(Path.Combine(outDir, Strings.Export.ImagesFolder, Strings.Export.ErrorFolder, Strings.Export.UnreadableFolder, "ur.png")));
        Assert.True(File.Exists(Path.Combine(outDir, Strings.Export.ImagesFolder, Strings.Export.DuplicateFolder, "du.png")));
    }

    [Fact]
    public void Export_failure_should_not_throw_and_should_return_failed_result_and_keep_audit_unchanged()
    {
        var tmp = MakeTempDir();
        var audit = BuildAuditResult(tmp, items: new[] { MakeItem(tmp, "a.png", "A", true, ErrorType.None) });

        var excelWriter = new ThrowingExcelWriter();
        var sut = new ExportService(excelWriter, new FileSystemService(), new FakeClock(DateTimeOffset.UtcNow));

        var beforeTotal = audit.Summary.Total;
        var result = sut.Export(audit, outputRoot: tmp);

        Assert.False(result.IsSuccess);
        Assert.Equal(beforeTotal, audit.Summary.Total);
    }

    [Fact]
    public void Repeat_export_same_audit_should_create_two_timestamp_dirs_and_same_report_content()
    {
        var tmp = MakeTempDir();
        var audit = BuildAuditResult(tmp, items: new[]
        {
            MakeItem(tmp, "a.png", "A", true, ErrorType.None),
            MakeItem(tmp, "b.png", "B", false, ErrorType.NotFound),
        });

        var clock = new SequenceClock(new[]
        {
            new DateTimeOffset(2026, 4, 25, 10, 0, 2, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 25, 10, 0, 3, TimeSpan.Zero),
        });

        var sut = new ExportService(new ClosedXmlExcelWriter(), new FileSystemService(), clock);

        var r1 = sut.Export(audit, outputRoot: tmp);
        var r2 = sut.Export(audit, outputRoot: tmp);

        Assert.True(r1.IsSuccess);
        Assert.True(r2.IsSuccess);
        Assert.NotEqual(r1.OutputPath, r2.OutputPath);

        var snap1 = SnapshotWorkbook(Path.Combine(r1.OutputPath!, Strings.Export.ReportFileName));
        var snap2 = SnapshotWorkbook(Path.Combine(r2.OutputPath!, Strings.Export.ReportFileName));
        Assert.Equal(snap1, snap2);
    }

    private static string MakeTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CogniLabel_Export_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static AuditResult BuildAuditResult(string root, IReadOnlyList<AuditItem> items)
    {
        var pass = items.Count(i => i.IsPass);
        var fail = items.Count - pass;

        return new AuditResult
        {
            Items = items,
            Summary = new AuditSummary { Total = items.Count, Pass = pass, Fail = fail },
            Errors = Array.Empty<AuditError>(),
            Meta = new AuditMeta { StartTime = DateTimeOffset.UtcNow, Stages = Array.Empty<StageResult>(), Cancelled = false },
        };
    }

    private static AuditItem MakeItem(string root, string fileName, string? sn, bool isPass, ErrorType error)
    {
        var sourcePath = Path.Combine(root, fileName);
        File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });

        var fields = new Dictionary<string, string?>();
        if (sn is not null)
            fields["SN"] = sn;

        return new AuditItem
        {
            Image = new ImageProcessResult
            {
                ImagePath = sourcePath,
                ImageName = fileName,
                Fields = fields,
                IsUnreadable = error == ErrorType.Unreadable,
            },
            IsPass = isPass,
            ErrorType = error,
            FieldIssues = Array.Empty<FieldIssue>(),
            ExcelValues = null,
        };
    }

    private sealed class ThrowingExcelWriter : IExcelWriter
    {
        public void WriteReport(string filePath, AuditResult auditResult, IReadOnlyList<AuditItem> items)
            => throw new IOException("locked");
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset now) { UtcNow = now; }
        public DateTimeOffset UtcNow { get; }
    }

    private sealed class SequenceClock : IClock
    {
        private readonly Queue<DateTimeOffset> _q;
        public SequenceClock(IEnumerable<DateTimeOffset> seq) => _q = new Queue<DateTimeOffset>(seq);
        public DateTimeOffset UtcNow => _q.Count > 0 ? _q.Dequeue() : DateTimeOffset.UtcNow;
    }

    private static string SnapshotWorkbook(string path)
    {
        using var wb = new XLWorkbook(path);
        var sheets = wb.Worksheets.Select(ws => ws.Name).OrderBy(x => x, StringComparer.Ordinal).ToList();

        var parts = new List<string>();
        foreach (var name in sheets)
        {
            var ws = wb.Worksheet(name);
            var used = ws.RangeUsed();
            parts.Add($"[{name}]");
            if (used is null)
                continue;

            foreach (var cell in used.Cells())
            {
                var addr = cell.Address.ToStringRelative(includeSheet: false);
                parts.Add($"{addr}={cell.GetString()}");
            }
        }

        return string.Join("\n", parts);
    }
}

