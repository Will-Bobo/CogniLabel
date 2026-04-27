using CogniLabel.Application;
using CogniLabel.Application.Dtos;
using CogniLabel.Application.Export;
using CogniLabel.Application.Pipeline;
using CogniLabel.Infrastructure.Excel;
using CogniLabel.Infrastructure.Export;
using CogniLabel.Infrastructure.IO;
using CogniLabel.Infrastructure.Templates;
using CogniLabel.Shared;
using ClosedXML.Excel;
using System.IO;

namespace CogniLabel.Tests.System;

public sealed class FaultInjectionTests
{
    [Fact]
    public async Task Invalid_excel_should_stop_pipeline_safely()
    {
        var root = MakeTempDir();
        var excelPath = Path.Combine(root, "bad.xlsx");
        File.WriteAllBytes(excelPath, new byte[] { 0, 1, 2, 3 }); // corrupt

        var imageDir = Path.Combine(root, "images");
        var templatePath = Path.Combine(root, "tpl.json");
        Directory.CreateDirectory(imageDir);
        File.WriteAllText(templatePath, """{ "Fields": [ { "Name": "SN", "X": 0, "Y": 0, "W": 1, "H": 1 } ] }""");

        var audit = new AuditService(new ClosedXmlExcelReader(), new TemplateLoader(), new ImageEnumerator(), new DefaultSingleImageProcessorFactory(_ => new DummyProc()));
        var r = await audit.RunAuditSafe(new AuditRequest
        {
            ExcelPath = excelPath,
            ImageFolderPath = imageDir,
            TemplatePath = templatePath,
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        }, null, CancellationToken.None);

        Assert.Contains(r.Meta.Stages, s => s.ShouldStop);
    }

    [Fact]
    public async Task Missing_image_folder_should_not_crash()
    {
        var root = MakeTempDir();
        var excelPath = Path.Combine(root, "input.xlsx");
        WriteExcel(excelPath, new[] { ("A", "1") });
        var templatePath = Path.Combine(root, "tpl.json");
        File.WriteAllText(templatePath, """{ "Fields": [ { "Name": "SN", "X": 0, "Y": 0, "W": 1, "H": 1 } ] }""");

        var audit = new AuditService(new ClosedXmlExcelReader(), new TemplateLoader(), new ImageEnumerator(), new DefaultSingleImageProcessorFactory(_ => new DummyProc()));
        var r = await audit.RunAuditSafe(new AuditRequest
        {
            ExcelPath = excelPath,
            ImageFolderPath = Path.Combine(root, "missing"),
            TemplatePath = templatePath,
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        }, null, CancellationToken.None);

        Assert.Contains(r.Meta.Stages, s => s.ShouldStop);
    }

    [Fact]
    public async Task Missing_some_images_should_mark_not_found_not_crash()
    {
        // Interpret as: some images produce SN not in Excel
        var root = MakeTempDir();
        var excelPath = Path.Combine(root, "input.xlsx");
        WriteExcel(excelPath, new[] { ("A", "1") });
        var imageDir = Path.Combine(root, "images");
        var templatePath = Path.Combine(root, "tpl.json");
        Directory.CreateDirectory(imageDir);
        File.WriteAllText(templatePath, """{ "Fields": [ { "Name": "SN", "X": 0, "Y": 0, "W": 1, "H": 1 } ] }""");

        File.WriteAllBytes(Path.Combine(imageDir, "A.png"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(imageDir, "B.png"), new byte[] { 1 });

        var audit = new AuditService(new ClosedXmlExcelReader(), new TemplateLoader(), new ImageEnumerator(),
            new DefaultSingleImageProcessorFactory(_ => new ProcFromName()));

        var r = await audit.RunAuditSafe(new AuditRequest
        {
            ExcelPath = excelPath,
            ImageFolderPath = imageDir,
            TemplatePath = templatePath,
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        }, null, CancellationToken.None);

        Assert.Equal(2, r.Summary.Total);
        Assert.Equal(1, r.Summary.Fail);
    }

    [Fact]
    public void Export_to_readonly_folder_should_fail_gracefully()
    {
        var root = MakeTempDir();
        var readonlyDir = Path.Combine(root, "ro");
        Directory.CreateDirectory(readonlyDir);

        var audit = new AuditResult
        {
            Items = Array.Empty<object>(),
            Summary = new AuditSummary { Total = 0, Pass = 0, Fail = 0 },
            Errors = Array.Empty<AuditError>(),
            Meta = new AuditMeta { StartTime = DateTimeOffset.UtcNow, Stages = Array.Empty<StageResult>(), Cancelled = false },
        };

        var fs = new ThrowingFs();
        var export = new ExportService(new ClosedXmlExcelWriter(), fs, new FakeClock(DateTimeOffset.UtcNow));
        var r = export.Export(audit, outputRoot: readonlyDir);

        Assert.False(r.IsSuccess);
    }

    [Fact]
    public void Export_partial_failure_should_not_leave_corrupted_state()
    {
        var root = MakeTempDir();

        var img = Path.Combine(root, "a.png");
        File.WriteAllBytes(img, new byte[] { 1 });

        var items = new[]
        {
            new CogniLabel.Application.Pipeline.AuditItem
            {
                Image = new CogniLabel.Application.SingleImage.ImageProcessResult
                {
                    ImagePath = img,
                    ImageName = "a.png",
                    Fields = new Dictionary<string, string?> { ["SN"]="A" },
                    IsUnreadable = true,
                },
                IsPass = false,
                ErrorType = CogniLabel.Shared.Enums.ErrorType.Unreadable,
                FieldIssues = Array.Empty<CogniLabel.Application.Pipeline.FieldIssue>(),
                ExcelValues = null,
            }
        };

        var audit = new AuditResult
        {
            Items = items,
            Summary = new AuditSummary { Total = 1, Pass = 0, Fail = 1 },
            Errors = Array.Empty<AuditError>(),
            Meta = new AuditMeta { StartTime = DateTimeOffset.UtcNow, Stages = Array.Empty<StageResult>(), Cancelled = false },
        };

        var fs = new CopyFailFs();
        var export = new ExportService(new ClosedXmlExcelWriter(), fs, new FakeClock(new DateTimeOffset(2026, 4, 25, 1, 1, 1, TimeSpan.Zero)));
        var r = export.Export(audit, outputRoot: root);
        Assert.False(r.IsSuccess);
        Assert.Equal(1, audit.Summary.Total);
    }

    [Fact]
    public async Task Invalid_request_should_not_crash_system()
    {
        var audit = new AuditService(new ClosedXmlExcelReader(), new TemplateLoader(), new ImageEnumerator(), new DefaultSingleImageProcessorFactory(_ => new DummyProc()));
        var r = await audit.RunAuditSafe(new AuditRequest
        {
            ExcelPath = null!,
            ImageFolderPath = "",
            TemplatePath = "???",
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        }, null, CancellationToken.None);

        Assert.Contains(r.Meta.Stages, s => s.ShouldStop);
        Assert.Contains(r.Errors, e => e.Message == Strings.Messages.InvalidRequest);
    }

    private sealed class DummyProc : ISingleImageProcessor
    {
        public CogniLabel.Application.SingleImage.ImageProcessResult ProcessSingleImage(string imagePath)
            => new()
            {
                ImagePath = imagePath,
                ImageName = Path.GetFileName(imagePath),
                Fields = new Dictionary<string, string?> { ["SN"] = "A" },
                IsUnreadable = false,
            };
    }

    private sealed class ProcFromName : ISingleImageProcessor
    {
        public CogniLabel.Application.SingleImage.ImageProcessResult ProcessSingleImage(string imagePath)
        {
            var sn = Path.GetFileNameWithoutExtension(imagePath);
            return new()
            {
                ImagePath = imagePath,
                ImageName = Path.GetFileName(imagePath),
                Fields = new Dictionary<string, string?> { ["SN"] = sn },
                IsUnreadable = false,
            };
        }
    }

    private sealed class ThrowingFs : IFileSystemService
    {
        public void CreateDirectory(string path) => throw new UnauthorizedAccessException();
        public void CopyFile(string sourcePath, string destPath, bool overwrite) => throw new UnauthorizedAccessException();
        public bool FileExists(string path) => true;
        public bool DirectoryExists(string path) => false;
    }

    private sealed class CopyFailFs : IFileSystemService
    {
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);
        public void CopyFile(string sourcePath, string destPath, bool overwrite) => throw new IOException("disk full");
        public bool FileExists(string path) => File.Exists(path);
        public bool DirectoryExists(string path) => Directory.Exists(path);
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

    private static string MakeTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CogniLabel_Fault_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

