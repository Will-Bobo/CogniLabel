using CogniLabel.Application;
using CogniLabel.Application.Dtos;
using CogniLabel.Application.Export;
using CogniLabel.Application.Pipeline;
using CogniLabel.Application.SingleImage;
using CogniLabel.Infrastructure.Excel;
using CogniLabel.Infrastructure.Export;
using CogniLabel.Infrastructure.IO;
using CogniLabel.Shared;
using ClosedXML.Excel;
using System.IO;

namespace CogniLabel.Tests.System;

public sealed class UxFlowTests
{
    [Fact]
    public async Task Mixed_errors_should_be_classified_correctly()
    {
        var root = MakeTempDir();
        var excelPath = Path.Combine(root, "input.xlsx");
        var imageDir = Path.Combine(root, "images");
        var templatePath = Path.Combine(root, "tpl.json");
        Directory.CreateDirectory(imageDir);

        WriteExcel(excelPath, new[] { ("A", "1"), ("B", "2") });
        File.WriteAllText(templatePath, """{ "Fields": [ { "Name": "SN", "X": 0, "Y": 0, "W": 1, "H": 1 }, { "Name": "X", "X": 0, "Y": 0, "W": 1, "H": 1 } ] }""");

        // A ok, B mismatch, C not found, D duplicate, E unreadable
        File.WriteAllBytes(Path.Combine(imageDir, "A.png"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(imageDir, "B.png"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(imageDir, "C.png"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(imageDir, "D1.png"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(imageDir, "D2.png"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(imageDir, "E.png"), new byte[] { 1 });

        var audit = new AuditService(
            new ClosedXmlExcelReader(),
            new CogniLabel.Infrastructure.Templates.TemplateLoader(),
            new ImageEnumerator(),
            new DefaultSingleImageProcessorFactory(_ => new ProcMixed()));

        var r = await audit.RunAuditSafe(new AuditRequest
        {
            ExcelPath = excelPath,
            ImageFolderPath = imageDir,
            TemplatePath = templatePath,
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN", ["X"] = "X" },
        }, null, CancellationToken.None);

        Assert.Equal(6, r.Summary.Total);
        Assert.True(r.Summary.Fail > 0);
    }

    [Fact]
    public async Task Pseudo_ui_flow_run_cancel_export_retry_should_be_operable()
    {
        var root = MakeTempDir();
        var excelPath = Path.Combine(root, "input.xlsx");
        var imageDir = Path.Combine(root, "images");
        var templatePath = Path.Combine(root, "tpl.json");
        Directory.CreateDirectory(imageDir);

        WriteExcel(excelPath, new[] { ("A", "1") });
        File.WriteAllText(templatePath, """{ "Fields": [ { "Name": "SN", "X": 0, "Y": 0, "W": 1, "H": 1 } ] }""");
        File.WriteAllBytes(Path.Combine(imageDir, "A.png"), new byte[] { 1 });

        var cts = new CancellationTokenSource();
        var progress = new List<ProgressInfo>();
        var audit = new AuditService(new ClosedXmlExcelReader(), new CogniLabel.Infrastructure.Templates.TemplateLoader(), new ImageEnumerator(),
            new DefaultSingleImageProcessorFactory(_ => new SlowProc(cts)));

        var runTask = audit.RunAuditSafe(new AuditRequest
        {
            ExcelPath = excelPath,
            ImageFolderPath = imageDir,
            TemplatePath = templatePath,
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        }, new Progress<ProgressInfo>(progress.Add), cts.Token);

        cts.Cancel();
        var partial = await runTask;
        Assert.True(partial.Meta.Cancelled);

        // retry should succeed
        var ok = await audit.RunAuditSafe(new AuditRequest
        {
            ExcelPath = excelPath,
            ImageFolderPath = imageDir,
            TemplatePath = templatePath,
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        }, null, CancellationToken.None);
        Assert.Equal(1, ok.Summary.Total);

        // export
        var export = new ExportService(new ClosedXmlExcelWriter(), new FileSystemService(), new FakeClock(new DateTimeOffset(2026, 4, 25, 12, 0, 1, TimeSpan.Zero)));
        var r = export.Export(ok, outputRoot: root);
        Assert.True(r.IsSuccess);
    }

    private sealed class ProcMixed : ISingleImageProcessor
    {
        public ImageProcessResult ProcessSingleImage(string imagePath)
        {
            var name = Path.GetFileNameWithoutExtension(imagePath);
            if (name == "E") // unreadable
            {
                return new ImageProcessResult { ImagePath = imagePath, ImageName = Path.GetFileName(imagePath), Fields = new Dictionary<string, string?> { ["SN"] = null }, IsUnreadable = true };
            }

            if (name.StartsWith("D", StringComparison.OrdinalIgnoreCase))
            {
                return new ImageProcessResult { ImagePath = imagePath, ImageName = Path.GetFileName(imagePath), Fields = new Dictionary<string, string?> { ["SN"] = "D" }, IsUnreadable = false };
            }

            if (name == "B") // mismatch on X
            {
                return new ImageProcessResult { ImagePath = imagePath, ImageName = Path.GetFileName(imagePath), Fields = new Dictionary<string, string?> { ["SN"] = "B", ["X"] = "999" }, IsUnreadable = false };
            }

            return new ImageProcessResult { ImagePath = imagePath, ImageName = Path.GetFileName(imagePath), Fields = new Dictionary<string, string?> { ["SN"] = name, ["X"] = "1" }, IsUnreadable = false };
        }
    }

    private sealed class SlowProc : ISingleImageProcessor
    {
        private readonly CancellationTokenSource _cts;
        public SlowProc(CancellationTokenSource cts) => _cts = cts;
        public ImageProcessResult ProcessSingleImage(string imagePath)
        {
            // simulate work; cancellation will cut off run at stage boundary
            Thread.Sleep(20);
            return new ImageProcessResult { ImagePath = imagePath, ImageName = Path.GetFileName(imagePath), Fields = new Dictionary<string, string?> { ["SN"] = "A" }, IsUnreadable = false };
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

    private static string MakeTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CogniLabel_UX_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

