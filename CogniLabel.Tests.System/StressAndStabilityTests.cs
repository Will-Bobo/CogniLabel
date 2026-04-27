using CogniLabel.Application;
using CogniLabel.Application.Dtos;
using CogniLabel.Application.Pipeline;
using CogniLabel.Infrastructure.Excel;
using CogniLabel.Infrastructure.Templates;
using ClosedXML.Excel;
using System.Diagnostics;
using System.IO;

namespace CogniLabel.Tests.System;

public sealed class StressAndStabilityTests
{
    [Fact]
    public async Task High_concurrency_should_not_break_order_or_results()
    {
        var root = MakeTempDir();
        var excelPath = Path.Combine(root, "input.xlsx");
        var imageDir = Path.Combine(root, "images");
        var templatePath = Path.Combine(root, "tpl.json");
        Directory.CreateDirectory(imageDir);

        var rows = Enumerable.Range(1, 200).Select(i => ($"S{i:D4}", i.ToString())).ToArray();
        WriteExcel(excelPath, rows);
        File.WriteAllText(templatePath, TemplateJson());

        foreach (var (sn, _) in rows)
            File.WriteAllBytes(Path.Combine(imageDir, $"{sn}.png"), new byte[] { 1 });

        var high = new FixedConcurrencyProvider(Math.Max(2, Environment.ProcessorCount * 2));
        var audit = SystemIntegrationTestsAccessor.CreateAuditService(new ClosedXmlExcelReader(), new TemplateLoader(), new CogniLabel.Infrastructure.IO.ImageEnumerator(), high);

        var req = new AuditRequest
        {
            ExcelPath = excelPath,
            ImageFolderPath = imageDir,
            TemplatePath = templatePath,
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        };

        var result = await audit.RunAuditSafe(req, progress: null, CancellationToken.None);

        Assert.Equal(200, result.Summary.Total);
        var items = result.Items.Cast<CogniLabel.Application.Pipeline.AuditItem>().ToList();
        var names = items.Select(i => i.Image.ImageName).ToList();
        Assert.Equal(names.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(), names);
    }

    [Fact]
    public async Task Cancellation_at_various_points_should_not_corrupt_state()
    {
        var root = MakeTempDir();
        var excelPath = Path.Combine(root, "input.xlsx");
        var imageDir = Path.Combine(root, "images");
        var templatePath = Path.Combine(root, "tpl.json");
        Directory.CreateDirectory(imageDir);

        var rows = Enumerable.Range(1, 50).Select(i => ($"S{i:D4}", i.ToString())).ToArray();
        WriteExcel(excelPath, rows);
        File.WriteAllText(templatePath, TemplateJson());
        foreach (var (sn, _) in rows)
            File.WriteAllBytes(Path.Combine(imageDir, $"{sn}.png"), new byte[] { 1 });

        var audit = SystemIntegrationTestsAccessor.CreateAuditService(new ClosedXmlExcelReader(), new TemplateLoader(), new CogniLabel.Infrastructure.IO.ImageEnumerator(), null);
        var req = new AuditRequest
        {
            ExcelPath = excelPath,
            ImageFolderPath = imageDir,
            TemplatePath = templatePath,
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        };

        // cancel before start
        using (var cts = new CancellationTokenSource())
        {
            cts.Cancel();
            var r = await audit.RunAuditSafe(req, progress: null, cts.Token);
            Assert.True(r.Meta.Cancelled);
        }

        // cancel mid-run: use progress hook
        var ctsMid = new CancellationTokenSource();
        var progressMid = new Progress<ProgressInfo>(p =>
        {
            if (!ctsMid.IsCancellationRequested && p.Stage == AuditStage.ImageProcessing && p.Current >= 5)
                ctsMid.Cancel();
        });

        var rMid = await audit.RunAuditSafe(req, progressMid, ctsMid.Token);
        Assert.True(rMid.Meta.Cancelled);

        // cancel near end
        var ctsEnd = new CancellationTokenSource();
        var progressEnd = new Progress<ProgressInfo>(p =>
        {
            if (!ctsEnd.IsCancellationRequested && p.Stage == AuditStage.Summary)
                ctsEnd.Cancel();
        });

        var rEnd = await audit.RunAuditSafe(req, progressEnd, ctsEnd.Token);
        Assert.True(rEnd.Meta.Cancelled || rEnd.Summary.Total == 50);
    }

    private sealed class FixedConcurrencyProvider : IConcurrencyProvider
    {
        private readonly int _n;
        public FixedConcurrencyProvider(int n) => _n = n;
        public int GetMaxConcurrency() => _n;
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
           { "Fields": [ { "Name": "SN", "X": 0.1, "Y": 0.1, "W": 0.8, "H": 0.1 }, { "Name": "X", "X": 0.1, "Y": 0.3, "W": 0.8, "H": 0.1 } ] }
           """;

    private static string MakeTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "CogniLabel_Stress_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

file static class SystemIntegrationTestsAccessor
{
    public static AuditService CreateAuditService(IExcelReader excel, ITemplateLoader tpl, IImageEnumerator imgs, IConcurrencyProvider? conc)
    {
        var factory = new DefaultSingleImageProcessorFactory(_ => new MockProc());
        return new AuditService(excel, tpl, imgs, factory, conc);
    }

    private sealed class MockProc : ISingleImageProcessor
    {
        public CogniLabel.Application.SingleImage.ImageProcessResult ProcessSingleImage(string imagePath)
        {
            var sn = Path.GetFileNameWithoutExtension(imagePath);
            return new CogniLabel.Application.SingleImage.ImageProcessResult
            {
                ImagePath = imagePath,
                ImageName = Path.GetFileName(imagePath),
                Fields = new Dictionary<string, string?> { ["SN"] = sn, ["X"] = "1" },
                IsUnreadable = false,
            };
        }
    }
}

