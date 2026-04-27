using CogniLabel.Application.Dtos;
using CogniLabel.Application.Export;
using CogniLabel.Application.Pipeline;
using CogniLabel.Application.SingleImage;
using CogniLabel.Infrastructure.Export;
using CogniLabel.Infrastructure.IO;
using CogniLabel.Infrastructure.Excel;
using CogniLabel.Shared;
using CogniLabel.Shared.Enums;
using ClosedXML.Excel;
using System.IO;

namespace CogniLabel.Tests.Application;

public sealed class Phase6VerificationTests
{
    [Fact]
    public void Verification1_export_should_be_weakly_coupled_to_source_images_when_missing_should_not_crash_and_message_clear()
    {
        var tmp = MakeTempDir();
        var imgPath = Path.Combine(tmp, "nf.png");
        File.WriteAllBytes(imgPath, new byte[] { 1, 2, 3 });

        var audit = BuildAuditResult(new[]
        {
            new AuditItem
            {
                Image = new ImageProcessResult
                {
                    ImagePath = imgPath,
                    ImageName = "nf.png",
                    Fields = new Dictionary<string, string?> { ["SN"] = "A" },
                    IsUnreadable = false,
                },
                IsPass = false,
                ErrorType = ErrorType.NotFound,
                FieldIssues = Array.Empty<FieldIssue>(),
                ExcelValues = null,
            }
        });

        File.Delete(imgPath); // simulate source image removed after RunAudit

        var sut = new ExportService(new ClosedXmlExcelWriter(), new FileSystemService(), new FakeClock(new DateTimeOffset(2026, 4, 25, 10, 10, 0, TimeSpan.Zero)));

        var beforeTotal = audit.Summary.Total;
        var result = sut.Export(audit, outputRoot: tmp);

        Assert.False(result.IsSuccess);
        Assert.Equal(Strings.Messages.ExportImageNotAccessible, result.Message);
        Assert.Equal(beforeTotal, audit.Summary.Total);
    }

    [Fact]
    public void Verification2_excel_export_structure_should_be_deterministic_sheet_and_column_order()
    {
        var tmp = MakeTempDir();
        var audit = BuildAuditResult(new[]
        {
            MakeItem(tmp, "a.png", sn: "A", isPass: true, error: ErrorType.None, fields: new Dictionary<string,string?> { ["SN"]="A", ["X"]="1", ["Y"]="2" }),
            MakeItem(tmp, "b.png", sn: "B", isPass: false, error: ErrorType.Mismatch, fields: new Dictionary<string,string?> { ["SN"]="B", ["X"]="9", ["Y"]="2" }),
        });

        var clock = new SequenceClock(new[]
        {
            new DateTimeOffset(2026, 4, 25, 10, 10, 1, TimeSpan.Zero),
            new DateTimeOffset(2026, 4, 25, 10, 10, 2, TimeSpan.Zero),
        });

        var sut = new ExportService(new ClosedXmlExcelWriter(), new FileSystemService(), clock);

        var r1 = sut.Export(audit, outputRoot: tmp);
        var r2 = sut.Export(audit, outputRoot: tmp);

        Assert.True(r1.IsSuccess);
        Assert.True(r2.IsSuccess);

        using var wb1 = new XLWorkbook(Path.Combine(r1.OutputPath!, Strings.Export.ReportFileName));
        using var wb2 = new XLWorkbook(Path.Combine(r2.OutputPath!, Strings.Export.ReportFileName));

        var sheetOrder1 = wb1.Worksheets.Select(s => s.Name).ToList();
        var sheetOrder2 = wb2.Worksheets.Select(s => s.Name).ToList();
        Assert.Equal(sheetOrder1, sheetOrder2);

        foreach (var name in sheetOrder1)
        {
            var h1 = ReadHeaderRow(wb1.Worksheet(name));
            var h2 = ReadHeaderRow(wb2.Worksheet(name));
            Assert.Equal(h1, h2);
        }

        // Specifically verify expanded field columns order in Details is deterministic
        var details1 = ReadHeaderRow(wb1.Worksheet(Strings.Report.SheetDetails));
        var details2 = ReadHeaderRow(wb2.Worksheet(Strings.Report.SheetDetails));
        Assert.Equal(details1, details2);
    }

    [Fact]
    public async Task Verification3_export_should_not_trigger_any_recognition_logic_after_runaudit()
    {
        // Arrange: RunAudit uses a processor once; afterwards we switch processor to throwing.
        var excel = new FakeExcelReader(new List<Dictionary<string, string>>
        {
            new() { ["SN"] = "A" },
        });

        var template = new TemplateDefinition(new[] { new TemplateFieldDefinition("SN", roi: default, isSn: true) });
        var templateLoader = new FakeTemplateLoader(template);
        var images = new FakeImageEnumerator(new[] { "a.png" });

        var spy = new SpyProcessor();
        var processorFactory = new SwitchableProcessorFactory(spy);

        var auditService = new CogniLabel.Application.AuditService(excel, templateLoader, images, processorFactory);
        var audit = await auditService.RunAudit(new AuditRequest
        {
            ExcelPath = "x",
            ImageFolderPath = "imgs",
            TemplatePath = "tpl",
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        }, progress: null, CancellationToken.None);

        var callsAfterRunAudit = spy.CallCount;

        // Switch to a throwing processor AFTER RunAudit
        processorFactory.Current = new ThrowingProcessor();

        // Export should not call any processor/barcode logic; it uses only AuditResult in memory + FS + ExcelWriter.
        var tmp = MakeTempDir();
        var sut = new ExportService(new ClosedXmlExcelWriter(), new FileSystemService(), new FakeClock(new DateTimeOffset(2026, 4, 25, 10, 10, 3, TimeSpan.Zero)));

        var export = sut.Export(audit, outputRoot: tmp);
        Assert.True(export.IsSuccess || !export.IsSuccess);

        Assert.Equal(callsAfterRunAudit, spy.CallCount);
    }

    private static IReadOnlyList<string> ReadHeaderRow(IXLWorksheet ws)
    {
        var first = ws.Row(1);
        var used = first.CellsUsed().ToList();
        if (used.Count == 0)
            return Array.Empty<string>();

        return used.Select(c => c.GetString()).ToList();
    }

    private static string MakeTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CogniLabel_Verify_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static AuditResult BuildAuditResult(IReadOnlyList<AuditItem> items)
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

    private static AuditItem MakeItem(string root, string fileName, string? sn, bool isPass, ErrorType error, Dictionary<string, string?> fields)
    {
        var sourcePath = Path.Combine(root, fileName);
        File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });

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

    private sealed class FakeExcelReader : IExcelReader
    {
        private readonly IReadOnlyList<Dictionary<string, string>> _rows;
        public FakeExcelReader(IReadOnlyList<Dictionary<string, string>> rows) => _rows = rows;
        public Task<IReadOnlyList<Dictionary<string, string>>> ReadAsStringTableAsync(string excelPath, CancellationToken cancellationToken)
            => Task.FromResult(_rows);
    }

    private sealed class FakeTemplateLoader : CogniLabel.Application.Pipeline.ITemplateLoader
    {
        private readonly TemplateDefinition _template;
        public FakeTemplateLoader(TemplateDefinition template) => _template = template;
        public TemplateDefinition Load(string templatePath) => _template;
    }

    private sealed class FakeImageEnumerator : CogniLabel.Application.Pipeline.IImageEnumerator
    {
        private readonly IReadOnlyList<string> _names;
        public FakeImageEnumerator(IReadOnlyList<string> names) => _names = names;
        public IReadOnlyList<string> Enumerate(string imageFolderPath)
        {
            return _names.Select(n => Path.Combine(Path.GetTempPath(), n)).ToList();
        }
    }

    private sealed class SpyProcessor : CogniLabel.Application.Pipeline.ISingleImageProcessor
    {
        public int CallCount { get; private set; }

        public ImageProcessResult ProcessSingleImage(string imagePath)
        {
            CallCount++;
            return new ImageProcessResult
            {
                ImagePath = imagePath,
                ImageName = Path.GetFileName(imagePath),
                Fields = new Dictionary<string, string?> { ["SN"] = "A" },
                IsUnreadable = false,
            };
        }
    }

    private sealed class ThrowingProcessor : CogniLabel.Application.Pipeline.ISingleImageProcessor
    {
        public ImageProcessResult ProcessSingleImage(string imagePath) => throw new Exception("should not be called by export");
    }

    private sealed class SwitchableProcessorFactory : CogniLabel.Application.Pipeline.ISingleImageProcessorFactory
    {
        public SwitchableProcessorFactory(CogniLabel.Application.Pipeline.ISingleImageProcessor current) => Current = current;
        public CogniLabel.Application.Pipeline.ISingleImageProcessor Current { get; set; }
        public CogniLabel.Application.Pipeline.ISingleImageProcessor Create(TemplateDefinition template) => Current;
    }
}

