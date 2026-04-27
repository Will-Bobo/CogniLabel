using CogniLabel.Application.Dtos;
using CogniLabel.Application.Pipeline;
using CogniLabel.Presentation.Commands;
using CogniLabel.Presentation.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CogniLabel.Presentation.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IAuditUseCase _audit;
    private readonly IExportUseCase _export;
    private readonly IDialogService? _dialogs;
    private readonly ITemplateEditorDialogService? _templateEditor;

    private AuditResult? _lastAuditResult;
    private CancellationTokenSource? _cts;

    private string _excelPath = string.Empty;
    private string _imageFolderPath = string.Empty;
    private string _templatePath = string.Empty;

    private bool _isRunning;
    private int _progressCurrent;
    private int _progressTotal;
    private AuditStage _currentStage;
    private string _currentMessage = string.Empty;
    private string _message = string.Empty;

    private int _total;
    private int _pass;
    private int _fail;

    private AuditItemViewModel? _selectedItem;

    public MainViewModel(
        IAuditUseCase audit,
        IExportUseCase export,
        IDialogService? dialogs = null,
        ITemplateEditorDialogService? templateEditor = null)
    {
        _audit = audit;
        _export = export;
        _dialogs = dialogs;
        _templateEditor = templateEditor;

        Items = new ObservableCollection<AuditItemViewModel>();

        RunAuditCommand = new AsyncRelayCommand(_ => RunAuditAsync(), _ => CanRunAudit());
        CancelCommand = new RelayCommand(_ => Cancel(), _ => IsRunning);
        ExportCommand = new RelayCommand(_ => Export(), _ => CanExport());

        BrowseExcelCommand = new RelayCommand(_ => BrowseExcel(), _ => _dialogs is not null && !IsRunning);
        BrowseImageFolderCommand = new RelayCommand(_ => BrowseImageFolder(), _ => _dialogs is not null && !IsRunning);
        BrowseTemplateCommand = new RelayCommand(_ => BrowseTemplate(), _ => _dialogs is not null && !IsRunning);
        CreateTemplateCommand = new RelayCommand(_ => CreateTemplate(), _ => _templateEditor is not null && !IsRunning);
        EditTemplateCommand = new RelayCommand(_ => EditTemplate(), _ => _templateEditor is not null && _dialogs is not null && !IsRunning);
    }

    public Dictionary<string, string> FieldMappings { get; } = new(StringComparer.Ordinal)
    {
        ["SN"] = "SN",
    };

    public string ExcelPath { get => _excelPath; set { if (Set(ref _excelPath, value)) RaiseCommandStates(); } }
    public string ImageFolderPath { get => _imageFolderPath; set { if (Set(ref _imageFolderPath, value)) RaiseCommandStates(); } }
    public string TemplatePath { get => _templatePath; set { if (Set(ref _templatePath, value)) RaiseCommandStates(); } }

    public bool IsRunning { get => _isRunning; private set { if (Set(ref _isRunning, value)) RaiseCommandStates(); } }

    public int ProgressCurrent { get => _progressCurrent; private set => Set(ref _progressCurrent, value); }
    public int ProgressTotal { get => _progressTotal; private set => Set(ref _progressTotal, value); }
    public AuditStage CurrentStage { get => _currentStage; private set => Set(ref _currentStage, value); }
    public string CurrentMessage { get => _currentMessage; private set => Set(ref _currentMessage, value); }
    public string Message { get => _message; private set => Set(ref _message, value); }

    public ObservableCollection<AuditItemViewModel> Items { get; }

    public int Total { get => _total; private set => Set(ref _total, value); }
    public int Pass { get => _pass; private set => Set(ref _pass, value); }
    public int Fail { get => _fail; private set => Set(ref _fail, value); }

    public AuditItemViewModel? SelectedItem { get => _selectedItem; set => Set(ref _selectedItem, value); }

    public AsyncRelayCommand RunAuditCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand BrowseExcelCommand { get; }
    public ICommand BrowseImageFolderCommand { get; }
    public ICommand BrowseTemplateCommand { get; }
    public ICommand CreateTemplateCommand { get; }
    public ICommand EditTemplateCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void OnProgress(ProgressInfo info)
    {
        ProgressCurrent = info.Current;
        ProgressTotal = info.Total;
        CurrentStage = info.Stage;
        CurrentMessage = info.Message;
    }

    private async Task RunAuditAsync()
    {
        if (!CanRunAudit())
            return;

        IsRunning = true;
        Items.Clear();
        Total = Pass = Fail = 0;
        _lastAuditResult = null;
        Message = string.Empty;

        _cts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<ProgressInfo>(OnProgress);
            var result = await _audit.RunAudit(new AuditRequest
            {
                ExcelPath = ExcelPath,
                ImageFolderPath = ImageFolderPath,
                TemplatePath = TemplatePath,
                FieldMappings = new Dictionary<string, string>(FieldMappings, StringComparer.Ordinal),
            }, progress, _cts.Token).ConfigureAwait(true);

            _lastAuditResult = result;
            ApplyAuditResult(result);

            if (result.Meta.Cancelled)
            {
                Message = "已取消";
            }
            else if (result.Errors.Count > 0)
            {
                Message = result.Errors[0].Message;
            }
            else
            {
                Message = "完成";
            }
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void ApplyAuditResult(AuditResult result)
    {
        Items.Clear();

        foreach (var obj in result.Items)
        {
            if (obj is CogniLabel.Application.Pipeline.AuditItem item)
                Items.Add(new AuditItemViewModel(item));
        }

        Total = result.Summary.Total;
        Pass = result.Summary.Pass;
        Fail = result.Summary.Fail;
    }

    private void Cancel()
    {
        _cts?.Cancel();
        Message = "已取消";
    }

    private void Export()
    {
        if (_lastAuditResult is null)
            return;

        var r = _export.Export(_lastAuditResult);
        Message = r.Message;
    }

    private void BrowseExcel()
    {
        var p = _dialogs?.PickExcelFile();
        if (!string.IsNullOrWhiteSpace(p))
            ExcelPath = p;
    }

    private void BrowseImageFolder()
    {
        var p = _dialogs?.PickImageFolder();
        if (!string.IsNullOrWhiteSpace(p))
            ImageFolderPath = p;
    }

    private void BrowseTemplate()
    {
        var p = _dialogs?.PickTemplateFile();
        if (!string.IsNullOrWhiteSpace(p))
            TemplatePath = p;
    }

    private void CreateTemplate()
    {
        var p = _templateEditor?.ShowCreate();
        if (!string.IsNullOrWhiteSpace(p))
            TemplatePath = p;
    }

    private void EditTemplate()
    {
        var loadPath = !string.IsNullOrWhiteSpace(TemplatePath)
            ? TemplatePath
            : _dialogs?.PickTemplateFile();
        if (string.IsNullOrWhiteSpace(loadPath))
            return;
        var p = _templateEditor?.ShowEdit(loadPath);
        if (!string.IsNullOrWhiteSpace(p))
            TemplatePath = p;
    }

    private bool CanRunAudit()
        => !IsRunning
           && !string.IsNullOrWhiteSpace(ExcelPath)
           && !string.IsNullOrWhiteSpace(ImageFolderPath)
           && !string.IsNullOrWhiteSpace(TemplatePath);

    private bool CanExport()
        => !IsRunning && _lastAuditResult is not null;

    private void RaiseCommandStates()
    {
        RunAuditCommand.RaiseCanExecuteChanged();
        if (CancelCommand is RelayCommand rc) rc.RaiseCanExecuteChanged();
        if (ExportCommand is RelayCommand ec) ec.RaiseCanExecuteChanged();
        if (BrowseExcelCommand is RelayCommand be) be.RaiseCanExecuteChanged();
        if (BrowseImageFolderCommand is RelayCommand bi) bi.RaiseCanExecuteChanged();
        if (BrowseTemplateCommand is RelayCommand bt) bt.RaiseCanExecuteChanged();
        if (CreateTemplateCommand is RelayCommand ct) ct.RaiseCanExecuteChanged();
        if (EditTemplateCommand is RelayCommand et) et.RaiseCanExecuteChanged();
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    // Test-only helper
    public void SetAuditResultForTest(AuditResult result)
    {
        _lastAuditResult = result;
    }
}

