using CogniLabel.Application;
using CogniLabel.Application.Dtos;
using CogniLabel.Application.Export;
using CogniLabel.Application.Pipeline;
using CogniLabel.Infrastructure.Excel;
using CogniLabel.Infrastructure.Export;
using CogniLabel.Infrastructure.IO;
using CogniLabel.Infrastructure.Templates;
using CogniLabel.Shared;
using CogniLabel.Shared.Enums;
using ClosedXML.Excel;
using System.Diagnostics;
using System.IO;

namespace CogniLabel.Tests.System;

public sealed class SystemIntegrationTests
{
    [Fact]
    public async Task Full_pipeline_real_dependencies_should_succeed()
    {
        var root = MakeTempDir();
        var excelPath = Path.Combine(root, "input.xlsx");
        var imageDir = Path.Combine(root, "images");
        var templatePath = Path.Combine(root, "tpl.json");
        Directory.CreateDirectory(imageDir);

        WriteExcel(excelPath, new[] { ("A", "1"), ("B", "2") });
        File.WriteAllText(templatePath, TemplateJson());
        File.WriteAllBytes(Path.Combine(imageDir, "a.png"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(imageDir, "b.png"), new byte[] { 2 });

        var audit = CreateAuditServiceForSystemTests(new ClosedXmlExcelReader(), new TemplateLoader(), new ImageEnumerator(), concurrency: null);
        var req = new AuditRequest
        {
            ExcelPath = excelPath,
            ImageFolderPath = imageDir,
            TemplatePath = templatePath,
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        };

        var result = await audit.RunAuditSafe(req, progress: null, CancellationToken.None);
        Assert.Equal(2, result.Summary.Total);
        Assert.Equal(2, result.Summary.Pass);
        Assert.Equal(0, result.Summary.Fail);

        var export = new ExportService(new ClosedXmlExcelWriter(), new FileSystemService(), new FakeClock(new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.Zero)));
        var exportResult = export.Export(result, outputRoot: root);

        Assert.True(exportResult.IsSuccess);
        Assert.NotNull(exportResult.OutputPath);
        Assert.True(File.Exists(Path.Combine(exportResult.OutputPath!, Strings.Export.ReportFileName)));
        Assert.True(File.Exists(Path.Combine(exportResult.OutputPath!, Strings.Export.ImagesFolder, Strings.Export.ErrorFolder, Strings.Export.NotFoundFolder, "b.png")) == false);
    }

    [Fact]
    public async Task Pipeline_100_items_should_complete_within_time_and_correct()
    {
        var root = MakeTempDir();
        var excelPath = Path.Combine(root, "input.xlsx");
        var imageDir = Path.Combine(root, "images");
        var templatePath = Path.Combine(root, "tpl.json");
        Directory.CreateDirectory(imageDir);

        var rows = Enumerable.Range(1, 100).Select(i => ($"S{i:D4}", i.ToString())).ToArray();
        WriteExcel(excelPath, rows);
        File.WriteAllText(templatePath, TemplateJson());

        foreach (var (sn, _) in rows)
            File.WriteAllBytes(Path.Combine(imageDir, $"{sn}.png"), new byte[] { 1 });

        var audit = CreateAuditServiceForSystemTests(new ClosedXmlExcelReader(), new TemplateLoader(), new ImageEnumerator(), concurrency: null);
        var req = new AuditRequest
        {
            ExcelPath = excelPath,
            ImageFolderPath = imageDir,
            TemplatePath = templatePath,
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        };

        var sw = Stopwatch.StartNew();
        var result = await audit.RunAuditSafe(req, progress: null, CancellationToken.None);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3));
        Assert.Equal(100, result.Summary.Total);
        Assert.Equal(100, result.Summary.Pass);
        Assert.Equal(0, result.Summary.Fail);
    }

    [Fact]
    public async Task Pipeline_1000_items_should_not_crash_and_be_deterministic()
    {
        var root = MakeTempDir();
        var excelPath = Path.Combine(root, "input.xlsx");
        var imageDir = Path.Combine(root, "images");
        var templatePath = Path.Combine(root, "tpl.json");
        Directory.CreateDirectory(imageDir);

        var rows = Enumerable.Range(1, 1000).Select(i => ($"S{i:D5}", i.ToString())).ToArray();
        WriteExcel(excelPath, rows);
        File.WriteAllText(templatePath, TemplateJson());

        foreach (var (sn, _) in rows)
            File.WriteAllBytes(Path.Combine(imageDir, $"{sn}.png"), new byte[] { 1 });

        var audit = CreateAuditServiceForSystemTests(new ClosedXmlExcelReader(), new TemplateLoader(), new ImageEnumerator(), concurrency: null);
        var req = new AuditRequest
        {
            ExcelPath = excelPath,
            ImageFolderPath = imageDir,
            TemplatePath = templatePath,
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        };

        var r1 = await audit.RunAuditSafe(req, progress: null, CancellationToken.None);
        var r2 = await audit.RunAuditSafe(req, progress: null, CancellationToken.None);

        Assert.Equal(r1.Summary.Total, r2.Summary.Total);
        Assert.Equal(r1.Summary.Pass, r2.Summary.Pass);
        Assert.Equal(r1.Summary.Fail, r2.Summary.Fail);
    }

    private static AuditService CreateAuditServiceForSystemTests(IExcelReader excel, ITemplateLoader template, IImageEnumerator images, IConcurrencyProvider? concurrency)
    {
        var factory = new DefaultSingleImageProcessorFactory(t => new MockSystemSingleImageProcessor(t));
        return new AuditService(excel, template, images, factory, concurrency);
    }

    private sealed class MockSystemSingleImageProcessor : ISingleImageProcessor
    {
        private readonly CogniLabel.Application.SingleImage.TemplateDefinition _template;
        public MockSystemSingleImageProcessor(CogniLabel.Application.SingleImage.TemplateDefinition template) => _template = template;

        public CogniLabel.Application.SingleImage.ImageProcessResult ProcessSingleImage(string imagePath)
        {
            var sn = Path.GetFileNameWithoutExtension(imagePath).ToUpperInvariant();
            var fields = new Dictionary<string, string?> { ["SN"] = sn, ["X"] = sn.Length.ToString() };
            return new CogniLabel.Application.SingleImage.ImageProcessResult
            {
                ImagePath = imagePath,
                ImageName = Path.GetFileName(imagePath),
                Fields = fields,
                IsUnreadable = false,
            };
        }
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset now) => UtcNow = now;
        public DateTimeOffset UtcNow { get; }
    }

    private static void WriteExcel(string path, (string sn, string x)[] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        ws.Cell(1, 1).Value = "SN";
        ws.Cell(1, 2).Value = "X";
        for (var i = 0; i < rows.Length; i++)
        {
            ws.Cell(i + 2, 1).Value = rows[i].sn;
            ws.Cell(i + 2, 2).Value = rows[i].x;
        }
        wb.SaveAs(path);
    }

    private static string TemplateJson()
        => """
           {
             "TemplateName": "T",
             "Fields": [
               { "Name": "SN", "Type": "BARCODE", "X": 0.1, "Y": 0.1, "W": 0.8, "H": 0.1 },
               { "Name": "X", "Type": "QR", "X": 0.1, "Y": 0.3, "W": 0.8, "H": 0.1 }
             ]
           }
           """;

    private static string MakeTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CogniLabel_System_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

