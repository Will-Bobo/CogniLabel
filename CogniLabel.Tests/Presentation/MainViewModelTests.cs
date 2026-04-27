using CogniLabel.Application.Dtos;
using CogniLabel.Application.Export;
using CogniLabel.Application.Pipeline;
using CogniLabel.Presentation.Services;
using CogniLabel.Presentation.ViewModels;
using CogniLabel.Shared;
using CogniLabel.Shared.Enums;
using CogniLabel.Application.SingleImage;
using System.Collections.ObjectModel;

namespace CogniLabel.Tests.Presentation;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task RunAuditCommand_should_call_usecase_and_toggle_IsRunning_and_update_items_and_summary()
    {
        var fakeAudit = new FakeAuditUseCase();
        fakeAudit.ResultToReturn = BuildAuditResult(total: 2, pass: 1, fail: 1);

        var vm = new MainViewModel(fakeAudit, new FakeExportUseCase());
        vm.ExcelPath = "a.xlsx";
        vm.ImageFolderPath = "imgs";
        vm.TemplatePath = "tpl.json";

        Assert.False(vm.IsRunning);
        await vm.RunAuditCommand.ExecuteAsync(null);

        Assert.Equal(1, fakeAudit.CallCount);
        Assert.False(vm.IsRunning);
        Assert.Equal(2, vm.Total);
        Assert.Equal(1, vm.Pass);
        Assert.Equal(1, vm.Fail);
        Assert.Equal(2, vm.Items.Count);
    }

    [Fact]
    public void Progress_update_should_reflect_in_viewmodel_state()
    {
        var fakeAudit = new FakeAuditUseCase();
        var vm = new MainViewModel(fakeAudit, new FakeExportUseCase());

        vm.OnProgress(new ProgressInfo { Current = 3, Total = 10, Stage = AuditStage.ImageProcessing, Message = "a.png" });

        Assert.Equal(3, vm.ProgressCurrent);
        Assert.Equal(10, vm.ProgressTotal);
        Assert.Equal(AuditStage.ImageProcessing, vm.CurrentStage);
        Assert.Equal("a.png", vm.CurrentMessage);
    }

    [Fact]
    public async Task CancelCommand_should_cancel_running_cts()
    {
        var fakeAudit = new FakeAuditUseCase { BlockUntilCancelled = true };
        var vm = new MainViewModel(fakeAudit, new FakeExportUseCase());
        vm.ExcelPath = "a.xlsx";
        vm.ImageFolderPath = "imgs";
        vm.TemplatePath = "tpl.json";

        var runTask = vm.RunAuditCommand.ExecuteAsync(null);
        Assert.True(vm.IsRunning);

        vm.CancelCommand.Execute(null);
        await runTask;

        Assert.True(fakeAudit.WasCancelled);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public void ExportCommand_should_call_export_usecase_and_not_call_runaudit()
    {
        var fakeAudit = new FakeAuditUseCase();
        var fakeExport = new FakeExportUseCase();
        var vm = new MainViewModel(fakeAudit, fakeExport);

        vm.SetAuditResultForTest(BuildAuditResult(total: 1, pass: 1, fail: 0));

        vm.ExportCommand.Execute(null);

        Assert.Equal(0, fakeAudit.CallCount);
        Assert.Equal(1, fakeExport.CallCount);
    }

    private static AuditResult BuildAuditResult(int total, int pass, int fail)
    {
        var items = new List<object>();
        for (var i = 0; i < total; i++)
        {
            items.Add(new AuditItem
            {
                Image = new ImageProcessResult
                {
                    ImagePath = $"c:\\fake\\{i}.png",
                    ImageName = $"{i}.png",
                    Fields = new Dictionary<string, string?> { ["SN"] = i.ToString() },
                    IsUnreadable = false,
                },
                IsPass = i < pass,
                ErrorType = i < pass ? ErrorType.None : ErrorType.NotFound,
                FieldIssues = Array.Empty<FieldIssue>(),
                ExcelValues = null,
            });
        }

        return new AuditResult
        {
            Items = items,
            Summary = new AuditSummary { Total = total, Pass = pass, Fail = fail },
            Errors = Array.Empty<AuditError>(),
            Meta = new AuditMeta { StartTime = DateTimeOffset.UtcNow, Stages = Array.Empty<StageResult>(), Cancelled = false },
        };
    }

    private sealed class FakeAuditUseCase : IAuditUseCase
    {
        public int CallCount { get; private set; }
        public AuditResult? ResultToReturn { get; set; }
        public bool BlockUntilCancelled { get; set; }
        public bool WasCancelled { get; private set; }

        public async Task<AuditResult> RunAudit(AuditRequest request, IProgress<ProgressInfo> progress, CancellationToken token)
        {
            CallCount++;
            progress.Report(new ProgressInfo { Current = 0, Total = 1, Stage = AuditStage.ExcelLoading, Message = string.Empty });

            if (BlockUntilCancelled)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), token);
                }
                catch (OperationCanceledException)
                {
                    WasCancelled = true;
                }
            }

            return ResultToReturn ?? BuildAuditResult(0, 0, 0);
        }
    }

    private sealed class FakeExportUseCase : IExportUseCase
    {
        public int CallCount { get; private set; }
        public ExportResult Export(AuditResult auditResult)
        {
            CallCount++;
            return new ExportResult { IsSuccess = true, OutputPath = "out", Message = Strings.Messages.ExportSuccess };
        }
    }
}

