using CogniLabel.Application;
using CogniLabel.Application.Dtos;
using CogniLabel.Application.Pipeline;
using CogniLabel.Application.SingleImage;
using CogniLabel.Infrastructure.Excel;
using CogniLabel.Shared.Enums;
using System.Collections.Concurrent;
using System.IO;

namespace CogniLabel.Tests.System;

[Trait("Category", "System")]
public sealed class HardeningTests
{
    [Fact]
    public async Task Progress_stage_sequence_should_be_monotonic_prefix_and_never_regress()
    {
        var excel = new FakeExcelReader(Enumerable.Range(1, 50).Select(i => new Dictionary<string, string> { ["SN"] = $"S{i:D4}" }).ToList());
        var template = new FakeTemplateLoader(new TemplateDefinition(new[] { new TemplateFieldDefinition("SN", default, isSn: true) }));
        var images = new FakeImageEnumerator(Enumerable.Range(1, 50).Select(i => $"c:\\fake\\S{i:D4}.png").Reverse().ToList());

        using var cts = new CancellationTokenSource();
        var procFactory = new CancelAfterNProcessorFactory(cts, cancelAfter: 3);
        var service = new AuditService(excel, template, images, procFactory);

        var events = new ConcurrentQueue<ProgressInfo>();
        var progress = new Progress<ProgressInfo>(p => events.Enqueue(p));

        var result = await service.RunAuditSafe(new AuditRequest
        {
            ExcelPath = "x",
            ImageFolderPath = "imgs",
            TemplatePath = "tpl",
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        }, progress, cts.Token);

        // Give background callbacks a chance; must not report after returning.
        var countAtReturn = events.Count;
        await Task.Delay(100);
        Assert.Equal(countAtReturn, events.Count);

        var allowed = new[]
        {
            AuditStage.ExcelLoading,
            AuditStage.ExcelValidating,
            AuditStage.TemplateLoading,
            AuditStage.ImageProcessing,
            AuditStage.Matching,
            AuditStage.Comparing,
            AuditStage.Deduplicating,
            AuditStage.Summary,
        };

        var index = 0;
        foreach (var stage in events.Select(e => e.Stage))
        {
            Assert.Contains(stage, allowed);

            // allow repeats within the same stage, but never regress
            var stageIndex = Array.IndexOf(allowed, stage);
            Assert.True(stageIndex >= index);
            index = stageIndex;
        }
    }

    [Fact]
    public async Task RunAuditSafe_should_never_throw_and_should_stop_on_dependency_exceptions()
    {
        var throwingExcel = new ThrowingExcelReader();
        var throwingTemplate = new ThrowingTemplateLoader();
        var throwingImages = new ThrowingImageEnumerator();
        var throwingFactory = new ThrowingProcessorFactory();

        // Excel throws first
        var s1 = new AuditService(throwingExcel, throwingTemplate, throwingImages, throwingFactory);
        var r1 = await s1.RunAuditSafe(new AuditRequest
        {
            ExcelPath = "x",
            ImageFolderPath = "imgs",
            TemplatePath = "tpl",
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        });
        Assert.Contains(r1.Meta.Stages, s => s.ShouldStop);

        // Template throws
        var s2 = new AuditService(new FakeExcelReader(new List<Dictionary<string, string>> { new() { ["SN"] = "A" } }), throwingTemplate, new FakeImageEnumerator(new[] { "a.png" }), new FakeProcessorFactory());
        var r2 = await s2.RunAuditSafe(new AuditRequest
        {
            ExcelPath = "x",
            ImageFolderPath = "imgs",
            TemplatePath = "tpl",
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        });
        Assert.Contains(r2.Meta.Stages, s => s.ShouldStop);

        // ImageEnumerator throws
        var s3 = new AuditService(new FakeExcelReader(new List<Dictionary<string, string>> { new() { ["SN"] = "A" } }), new FakeTemplateLoader(new TemplateDefinition(new[] { new TemplateFieldDefinition("SN", default, true) })), throwingImages, new FakeProcessorFactory());
        var r3 = await s3.RunAuditSafe(new AuditRequest
        {
            ExcelPath = "x",
            ImageFolderPath = "imgs",
            TemplatePath = "tpl",
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        });
        Assert.Contains(r3.Meta.Stages, s => s.ShouldStop);

        // ProcessorFactory throws
        var s4 = new AuditService(new FakeExcelReader(new List<Dictionary<string, string>> { new() { ["SN"] = "A" } }), new FakeTemplateLoader(new TemplateDefinition(new[] { new TemplateFieldDefinition("SN", default, true) })), new FakeImageEnumerator(new[] { "a.png" }), throwingFactory);
        var r4 = await s4.RunAuditSafe(new AuditRequest
        {
            ExcelPath = "x",
            ImageFolderPath = "imgs",
            TemplatePath = "tpl",
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        });
        Assert.Contains(r4.Meta.Stages, s => s.ShouldStop);
    }

    [Fact]
    public async Task High_concurrency_multiple_runs_should_be_completely_deterministic()
    {
        var excelRows = Enumerable.Range(1, 50).Select(i => new Dictionary<string, string> { ["SN"] = $"S{i:D4}" }).ToList();
        var excel = new FakeExcelReader(excelRows);
        var template = new FakeTemplateLoader(new TemplateDefinition(new[] { new TemplateFieldDefinition("SN", default, isSn: true) }));
        var images = new FakeImageEnumerator(Enumerable.Range(1, 50).Select(i => $"c:\\fake\\S{i:D4}.png").OrderBy(_ => Guid.NewGuid()).ToList());

        var conc = new FixedConcurrencyProvider(8);
        var service = new AuditService(excel, template, images, new DeterministicProcessorFactory(), conc);

        string? snap = null;
        for (var i = 0; i < 5; i++)
        {
            var r = await service.RunAuditSafe(new AuditRequest
            {
                ExcelPath = "x",
                ImageFolderPath = "imgs",
                TemplatePath = "tpl",
                FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
            });

            var s = Snapshot(r);
            snap ??= s;
            Assert.Equal(snap, s);
        }
    }

    [Fact]
    public async Task Cancellation_should_keep_summary_self_consistent()
    {
        var excel = new FakeExcelReader(Enumerable.Range(1, 50).Select(i => new Dictionary<string, string> { ["SN"] = $"S{i:D4}" }).ToList());
        var template = new FakeTemplateLoader(new TemplateDefinition(new[] { new TemplateFieldDefinition("SN", default, isSn: true) }));
        var images = new FakeImageEnumerator(Enumerable.Range(1, 50).Select(i => $"c:\\fake\\S{i:D4}.png").ToList());

        using var cts = new CancellationTokenSource();
        var procFactory = new CancelAfterNProcessorFactory(cts, cancelAfter: 5);
        var service = new AuditService(excel, template, images, procFactory, new FixedConcurrencyProvider(8));

        var r = await service.RunAuditSafe(new AuditRequest
        {
            ExcelPath = "x",
            ImageFolderPath = "imgs",
            TemplatePath = "tpl",
            FieldMappings = new Dictionary<string, string> { ["SN"] = "SN" },
        }, progress: null, cts.Token);

        Assert.True(r.Meta.Cancelled);
        Assert.Equal(r.Summary.Total, r.Summary.Pass + r.Summary.Fail);
    }

    private static string Snapshot(AuditResult r)
    {
        var items = r.Items.Cast<CogniLabel.Application.Pipeline.AuditItem>().ToList();
        return string.Join("\n", new[]
        {
            $"T={r.Summary.Total};P={r.Summary.Pass};F={r.Summary.Fail};C={r.Meta.Cancelled}",
            string.Join("|", items.Select(i => $"{i.Image.ImageName}:{i.ErrorType}:{(i.IsPass ? "P" : "F")}")),
        });
    }

    private sealed class FixedConcurrencyProvider : IConcurrencyProvider
    {
        private readonly int _n;
        public FixedConcurrencyProvider(int n) => _n = n;
        public int GetMaxConcurrency() => _n;
    }

    private sealed class FakeExcelReader : IExcelReader
    {
        private readonly IReadOnlyList<Dictionary<string, string>> _rows;
        public FakeExcelReader(IReadOnlyList<Dictionary<string, string>> rows) => _rows = rows;
        public Task<IReadOnlyList<Dictionary<string, string>>> ReadAsStringTableAsync(string excelPath, CancellationToken cancellationToken) => Task.FromResult(_rows);
    }

    private sealed class ThrowingExcelReader : IExcelReader
    {
        public Task<IReadOnlyList<Dictionary<string, string>>> ReadAsStringTableAsync(string excelPath, CancellationToken cancellationToken)
            => throw new InvalidOperationException("excel");
    }

    private sealed class FakeTemplateLoader : ITemplateLoader
    {
        private readonly TemplateDefinition _tpl;
        public FakeTemplateLoader(TemplateDefinition tpl) => _tpl = tpl;
        public TemplateDefinition Load(string templatePath) => _tpl;
    }

    private sealed class ThrowingTemplateLoader : ITemplateLoader
    {
        public TemplateDefinition Load(string templatePath) => throw new InvalidOperationException("tpl");
    }

    private sealed class FakeImageEnumerator : IImageEnumerator
    {
        private readonly IReadOnlyList<string> _paths;
        public FakeImageEnumerator(IEnumerable<string> paths) => _paths = paths.ToList();
        public IReadOnlyList<string> Enumerate(string imageFolderPath) => _paths;
    }

    private sealed class ThrowingImageEnumerator : IImageEnumerator
    {
        public IReadOnlyList<string> Enumerate(string imageFolderPath) => throw new InvalidOperationException("imgs");
    }

    private sealed class FakeProcessorFactory : ISingleImageProcessorFactory
    {
        public ISingleImageProcessor Create(TemplateDefinition template) => new DeterministicProcessor();
    }

    private sealed class ThrowingProcessorFactory : ISingleImageProcessorFactory
    {
        public ISingleImageProcessor Create(TemplateDefinition template) => throw new InvalidOperationException("factory");
    }

    private sealed class DeterministicProcessorFactory : ISingleImageProcessorFactory
    {
        public ISingleImageProcessor Create(TemplateDefinition template) => new DeterministicProcessor();
    }

    private sealed class DeterministicProcessor : ISingleImageProcessor
    {
        public ImageProcessResult ProcessSingleImage(string imagePath)
        {
            var sn = Path.GetFileNameWithoutExtension(imagePath);
            return new ImageProcessResult
            {
                ImagePath = imagePath,
                ImageName = Path.GetFileName(imagePath),
                Fields = new Dictionary<string, string?> { ["SN"] = sn },
                IsUnreadable = false,
            };
        }
    }

    private sealed class CancelAfterNProcessorFactory : ISingleImageProcessorFactory
    {
        private readonly CancellationTokenSource _cts;
        private readonly int _cancelAfter;
        private int _count;

        public CancelAfterNProcessorFactory(CancellationTokenSource cts, int cancelAfter)
        {
            _cts = cts;
            _cancelAfter = cancelAfter;
        }

        public ISingleImageProcessor Create(TemplateDefinition template) => new Proc(this);

        private sealed class Proc : ISingleImageProcessor
        {
            private readonly CancelAfterNProcessorFactory _owner;
            public Proc(CancelAfterNProcessorFactory owner) => _owner = owner;

            public ImageProcessResult ProcessSingleImage(string imagePath)
            {
                var n = Interlocked.Increment(ref _owner._count);
                if (n == _owner._cancelAfter)
                    _owner._cts.Cancel();

                // Make cancellation race visible: keep other tasks running briefly
                Thread.Sleep(30);

                // keep deterministic output
                var sn = Path.GetFileNameWithoutExtension(imagePath);
                return new ImageProcessResult
                {
                    ImagePath = imagePath,
                    ImageName = Path.GetFileName(imagePath),
                    Fields = new Dictionary<string, string?> { ["SN"] = sn },
                    IsUnreadable = false,
                };
            }
        }
    }
}

