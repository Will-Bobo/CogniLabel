using CogniLabel.Application;
using CogniLabel.Application.Dtos;
using CogniLabel.Application.Pipeline;
using CogniLabel.Application.SingleImage;
using CogniLabel.Infrastructure.Excel;
using CogniLabel.Shared.Enums;
using System.IO;

namespace CogniLabel.Tests.Application;

public sealed class RunAuditPipelineTests
{
    [Fact]
    public async Task Happy_path_should_run_full_pipeline_and_return_summary_and_items()
    {
        var excel = new FakeExcelReader(new List<Dictionary<string, string>>
        {
            new() { ["SN_CODE"] = "A", ["X_COL"] = "1" },
            new() { ["SN_CODE"] = "B", ["X_COL"] = "2" },
        });

        var template = new TemplateDefinition(new[]
        {
            new TemplateFieldDefinition("SN", roi: default, isSn: true),
            new TemplateFieldDefinition("X", roi: default, isSn: false),
        });

        var templateLoader = new FakeTemplateLoader(template);
        var images = new FakeImageEnumerator(new[] { "b.png", "a.png" }); // intentionally unordered

        var processorFactory = new FakeProcessorFactory(path => path.Contains("a.png")
            ? new ImageProcessResult { ImagePath = path, ImageName = "a.png", Fields = new Dictionary<string, string?> { ["SN"] = "A", ["X"] = "1" }, IsUnreadable = false }
            : new ImageProcessResult { ImagePath = path, ImageName = "b.png", Fields = new Dictionary<string, string?> { ["SN"] = "B", ["X"] = "2" }, IsUnreadable = false });

        var service = new AuditService(excel, templateLoader, images, processorFactory);
        var result = await service.RunAudit(new AuditRequest
        {
            ExcelPath = "in-memory.xlsx",
            ImageFolderPath = "in-memory-images",
            TemplatePath = "in-memory-template.json",
            FieldMappings = new Dictionary<string, string>
            {
                ["SN"] = "SN_CODE",
                ["X"] = "X_COL",
            },
        }, progress: null, CancellationToken.None);

        Assert.Equal(2, result.Summary.Total);
        Assert.Equal(2, result.Summary.Pass);
        Assert.Equal(0, result.Summary.Fail);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task Excel_validation_failure_should_stop_and_not_execute_later_stages()
    {
        var excel = new FakeExcelReader(new List<Dictionary<string, string>>
        {
            new() { ["SN"] = "A" },
            new() { ["SN"] = "A" },
        });

        var templateLoader = new CountingTemplateLoader(new FakeTemplateLoader(new TemplateDefinition(Array.Empty<TemplateFieldDefinition>())));
        var images = new CountingImageEnumerator(new FakeImageEnumerator(new[] { "a.png" }));
        var processorFactory = new CountingProcessorFactory(new FakeProcessorFactory(_ => throw new Exception("should not run")));

        var service = new AuditService(excel, templateLoader, images, processorFactory);
        var result = await service.RunAudit(new AuditRequest
        {
            ExcelPath = "in-memory.xlsx",
            ImageFolderPath = "in-memory-images",
            TemplatePath = "in-memory-template.json",
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        }, progress: null, CancellationToken.None);

        Assert.Contains(result.Meta.Stages, s => s.ShouldStop);
        Assert.Equal(0, templateLoader.LoadCount);
        Assert.Equal(0, images.EnumerateCount);
        Assert.Equal(0, processorFactory.CreateCount);
    }

    [Fact]
    public async Task Concurrency_determinism_same_input_three_runs_should_be_identical()
    {
        var excel = new FakeExcelReader(new List<Dictionary<string, string>>
        {
            new() { ["SN"] = "A" },
            new() { ["SN"] = "B" },
            new() { ["SN"] = "C" },
        });

        var template = new TemplateDefinition(new[]
        {
            new TemplateFieldDefinition("SN", roi: default, isSn: true),
        });

        var templateLoader = new FakeTemplateLoader(template);
        var images = new FakeImageEnumerator(new[] { "c.png", "a.png", "b.png" });

        var processorFactory = new FakeProcessorFactory(path =>
        {
            // deterministic output, but caller may run tasks in different timing
            var name = Path.GetFileName(path);
            var sn = name[..1].ToUpperInvariant();
            return new ImageProcessResult { ImagePath = path, ImageName = name, Fields = new Dictionary<string, string?> { ["SN"] = sn }, IsUnreadable = false };
        });

        var service = new AuditService(excel, templateLoader, images, processorFactory);
        var r1 = await service.RunAudit(MakeRequest(), null, CancellationToken.None);
        var r2 = await service.RunAudit(MakeRequest(), null, CancellationToken.None);
        var r3 = await service.RunAudit(MakeRequest(), null, CancellationToken.None);

        Assert.Equal(r1.Summary.Total, r2.Summary.Total);
        Assert.Equal(r1.Summary.Pass, r2.Summary.Pass);
        Assert.Equal(r1.Summary.Fail, r2.Summary.Fail);
        Assert.Equal(string.Join("|", r1.Errors.Select(e => e.Type)), string.Join("|", r2.Errors.Select(e => e.Type)));

        Assert.Equal(r1.Summary.Total, r3.Summary.Total);
        Assert.Equal(r1.Summary.Pass, r3.Summary.Pass);
        Assert.Equal(r1.Summary.Fail, r3.Summary.Fail);

        AuditRequest MakeRequest() => new()
        {
            ExcelPath = "in-memory.xlsx",
            ImageFolderPath = "in-memory-images",
            TemplatePath = "in-memory-template.json",
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        };
    }

    [Fact]
    public async Task Output_order_should_be_stable_by_file_name()
    {
        var excel = new FakeExcelReader(new List<Dictionary<string, string>>
        {
            new() { ["SN"] = "A" },
            new() { ["SN"] = "B" },
        });

        var template = new TemplateDefinition(new[] { new TemplateFieldDefinition("SN", roi: default, isSn: true) });
        var service = new AuditService(excel, new FakeTemplateLoader(template), new FakeImageEnumerator(new[] { "b.png", "a.png" }),
            new FakeProcessorFactory(path =>
            {
                var name = Path.GetFileName(path);
                var sn = name[..1].ToUpperInvariant();
            return new ImageProcessResult { ImagePath = path, ImageName = name, Fields = new Dictionary<string, string?> { ["SN"] = sn }, IsUnreadable = false };
            }));

        var result = await service.RunAudit(new AuditRequest
        {
            ExcelPath = "x",
            ImageFolderPath = "imgs",
            TemplatePath = "tpl",
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        }, null, CancellationToken.None);

        var items = result.Items.Cast<AuditItem>().ToList();
        Assert.Equal(new[] { "a.png", "b.png" }, items.Select(i => i.Image.ImageName));
    }

    [Fact]
    public async Task Cancellation_should_return_partial_results_and_set_meta_cancelled_true()
    {
        var excel = new FakeExcelReader(new List<Dictionary<string, string>>
        {
            new() { ["SN"] = "A" },
            new() { ["SN"] = "B" },
            new() { ["SN"] = "C" },
        });

        var template = new TemplateDefinition(new[] { new TemplateFieldDefinition("SN", roi: default, isSn: true) });
        var templateLoader = new FakeTemplateLoader(template);
        var images = new FakeImageEnumerator(new[] { "a.png", "b.png", "c.png" });

        var cts = new CancellationTokenSource();
        var processorFactory = new FakeProcessorFactory(path =>
        {
            if (Path.GetFileName(path) == "b.png")
                cts.Cancel();
            return new ImageProcessResult { ImagePath = path, ImageName = Path.GetFileName(path), Fields = new Dictionary<string, string?> { ["SN"] = Path.GetFileName(path)![..1].ToUpperInvariant() }, IsUnreadable = false };
        });

        var service = new AuditService(excel, templateLoader, images, processorFactory);
        var result = await service.RunAudit(new AuditRequest
        {
            ExcelPath = "x",
            ImageFolderPath = "imgs",
            TemplatePath = "tpl",
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        }, null, cts.Token);

        Assert.True(result.Meta.Cancelled);
        Assert.True(result.Items.Count <= 3);
    }

    [Fact]
    public async Task Single_image_failure_should_not_affect_others_and_that_item_is_unreadable()
    {
        var excel = new FakeExcelReader(new List<Dictionary<string, string>>
        {
            new() { ["SN"] = "A" },
            new() { ["SN"] = "B" },
        });

        var template = new TemplateDefinition(new[] { new TemplateFieldDefinition("SN", roi: default, isSn: true) });
        var processorFactory = new FakeProcessorFactory(path =>
        {
            if (Path.GetFileName(path) == "a.png")
                throw new InvalidOperationException("boom");
            return new ImageProcessResult { ImagePath = path, ImageName = Path.GetFileName(path), Fields = new Dictionary<string, string?> { ["SN"] = "B" }, IsUnreadable = false };
        });

        var service = new AuditService(excel, new FakeTemplateLoader(template), new FakeImageEnumerator(new[] { "a.png", "b.png" }), processorFactory);
        var result = await service.RunAudit(new AuditRequest
        {
            ExcelPath = "x",
            ImageFolderPath = "imgs",
            TemplatePath = "tpl",
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        }, null, CancellationToken.None);

        var items = result.Items.Cast<AuditItem>().ToList();
        Assert.Equal(2, items.Count);
        Assert.True(items.Single(i => i.Image.ImageName == "a.png").ErrorType == ErrorType.Unreadable);
        Assert.True(items.Single(i => i.Image.ImageName == "b.png").ErrorType != ErrorType.Unreadable);
    }

    [Fact]
    public async Task Progress_should_receive_multiple_updates_with_stage_changes()
    {
        var excel = new FakeExcelReader(new List<Dictionary<string, string>>
        {
            new() { ["SN"] = "A" },
        });

        var template = new TemplateDefinition(new[] { new TemplateFieldDefinition("SN", roi: default, isSn: true) });
        var service = new AuditService(excel, new FakeTemplateLoader(template), new FakeImageEnumerator(new[] { "a.png" }),
            new FakeProcessorFactory(p => new ImageProcessResult { ImagePath = p, ImageName = "a.png", Fields = new Dictionary<string, string?> { ["SN"] = "A" }, IsUnreadable = false }));

        var updates = new List<ProgressInfo>();
        var progress = new Progress<ProgressInfo>(updates.Add);

        _ = await service.RunAudit(new AuditRequest
        {
            ExcelPath = "x",
            ImageFolderPath = "imgs",
            TemplatePath = "tpl",
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        }, progress, CancellationToken.None);

        Assert.True(updates.Count >= 3);
        Assert.Contains(updates, u => u.Stage == AuditStage.ExcelLoading);
        Assert.Contains(updates, u => u.Stage == AuditStage.ImageProcessing);
        Assert.Contains(updates, u => u.Stage == AuditStage.Summary);
    }

    private sealed class FakeExcelReader : IExcelReader
    {
        private readonly IReadOnlyList<Dictionary<string, string>> _rows;
        public FakeExcelReader(IReadOnlyList<Dictionary<string, string>> rows) => _rows = rows;
        public Task<IReadOnlyList<Dictionary<string, string>>> ReadAsStringTableAsync(string excelPath, CancellationToken cancellationToken)
            => Task.FromResult(_rows);
    }

    private sealed class FakeTemplateLoader : ITemplateLoader
    {
        private readonly TemplateDefinition _template;
        public FakeTemplateLoader(TemplateDefinition template) => _template = template;
        public TemplateDefinition Load(string templatePath) => _template;
    }

    private sealed class FakeImageEnumerator : IImageEnumerator
    {
        private readonly IReadOnlyList<string> _names;
        public FakeImageEnumerator(IReadOnlyList<string> names) => _names = names;
        public IReadOnlyList<string> Enumerate(string imageFolderPath) => _names.Select(n => $"c:\\fake\\{n}").ToList();
    }

    private sealed class FakeProcessorFactory : ISingleImageProcessorFactory
    {
        private readonly Func<string, ImageProcessResult> _fn;
        public FakeProcessorFactory(Func<string, ImageProcessResult> fn) => _fn = fn;
        public ISingleImageProcessor Create(TemplateDefinition template) => new Proc(_fn);
        private sealed class Proc : ISingleImageProcessor
        {
            private readonly Func<string, ImageProcessResult> _fn;
            public Proc(Func<string, ImageProcessResult> fn) => _fn = fn;
            public ImageProcessResult ProcessSingleImage(string imagePath) => _fn(imagePath);
        }
    }

    private sealed class CountingTemplateLoader : ITemplateLoader
    {
        private readonly ITemplateLoader _inner;
        public int LoadCount { get; private set; }
        public CountingTemplateLoader(ITemplateLoader inner) => _inner = inner;
        public TemplateDefinition Load(string templatePath) { LoadCount++; return _inner.Load(templatePath); }
    }

    private sealed class CountingImageEnumerator : IImageEnumerator
    {
        private readonly IImageEnumerator _inner;
        public int EnumerateCount { get; private set; }
        public CountingImageEnumerator(IImageEnumerator inner) => _inner = inner;
        public IReadOnlyList<string> Enumerate(string imageFolderPath) { EnumerateCount++; return _inner.Enumerate(imageFolderPath); }
    }

    private sealed class CountingProcessorFactory : ISingleImageProcessorFactory
    {
        private readonly ISingleImageProcessorFactory _inner;
        public int CreateCount { get; private set; }
        public CountingProcessorFactory(ISingleImageProcessorFactory inner) => _inner = inner;
        public ISingleImageProcessor Create(TemplateDefinition template) { CreateCount++; return _inner.Create(template); }
    }
}

