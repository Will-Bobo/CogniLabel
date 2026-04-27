using CogniLabel.Application.Pipeline;
using CogniLabel.Application.SingleImage;
using CogniLabel.Application;
using CogniLabel.Core.Roi;
using CogniLabel.Presentation.Commands;
using CogniLabel.Presentation.Services;
using CogniLabel.Shared;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CogniLabel.Presentation.ViewModels;

public enum TemplateEditorMode
{
    Create,
    Edit,
}

public sealed class TemplateEditorViewModel : INotifyPropertyChanged
{
    private readonly ITemplateLoader _loader;
    private readonly ITemplateWriter _writer;
    private readonly IDialogService _dialogs;
    private readonly RoiStateService _roiState;
    private readonly TemplateEditorMode _mode;
    private readonly string? _initialPath;

    private TemplateEditorFieldRowViewModel? _selectedField;
    private string _editorMessage = string.Empty;
    private bool _isBatchUpdating;
    private readonly HashSet<Guid> _initializedRows = new();
    private readonly Dictionary<Guid, TemplateEditorFieldRowViewModel> _rowVmCache = new();

    private TemplateEditorFieldRowViewModel GetOrCreateRowVM(Guid rowId)
    {
        if (_rowVmCache.TryGetValue(rowId, out var existing))
        {
            if (Debugger.IsAttached)
                Debug.WriteLine($"[ROW VM CACHE HIT] rowId={rowId}, vmHash={existing.GetHashCode()}");
            return existing;
        }

        var vm = new TemplateEditorFieldRowViewModel(rowId);
        _rowVmCache[rowId] = vm;

        if (Debugger.IsAttached)
            Debug.WriteLine($"[ROW VM CACHE MISS / NEW] rowId={rowId}, vmHash={vm.GetHashCode()}");

        return vm;
    }

    private void TrackRowVmFromCollection(TemplateEditorFieldRowViewModel row)
    {
        if (_rowVmCache.TryGetValue(row.RowId, out var cached))
        {
            if (!ReferenceEquals(cached, row) && Debugger.IsAttached)
            {
                Debug.WriteLine(
                    $"[ROW VM DUPLICATE INSTANCE DETECTED] rowId={row.RowId}, cachedHash={cached.GetHashCode()}, newHash={row.GetHashCode()}");
            }
            return;
        }

        _rowVmCache[row.RowId] = row;
        if (Debugger.IsAttached)
            Debug.WriteLine($"[ROW VM CACHE MISS / EXTERNAL] rowId={row.RowId}, vmHash={row.GetHashCode()}");
    }

    public TemplateEditorViewModel(
        ITemplateLoader loader,
        ITemplateWriter writer,
        IDialogService dialogs,
        RoiStateService roiState,
        TemplateEditorMode mode,
        string? initialPathForEdit = null)
    {
        _loader = loader;
        _writer = writer;
        _dialogs = dialogs;
        _roiState = roiState;
        _roiState.RoiChanged += OnRoiChanged;
        _mode = mode;
        _initialPath = initialPathForEdit;

        Fields = new ObservableCollection<TemplateEditorFieldRowViewModel>();
        Fields.CollectionChanged += OnFieldsCollectionChanged;

        AddFieldCommand = new RelayCommand(_ => AddField(), _ => true);
        RemoveFieldCommand = new RelayCommand(_ => RemoveField(), _ => SelectedField is not null);
        SaveCommand = new RelayCommand(_ => Save(), _ => CanSave());
        CancelCommand = new RelayCommand(_ => Cancel(), _ => true);

        using (_roiState.BeginInitialization())
        {
            if (_mode == TemplateEditorMode.Edit && !string.IsNullOrWhiteSpace(_initialPath))
                TryLoadTemplate(_initialPath);
            else
                EnsureDefaultFirstRow();

            if (SelectedField is null && Fields.Count > 0)
                SelectedField = Fields[0];

            // 初始化阶段：将当前 state 静默同步到展示层（不触发 RoiChanged storm）
            foreach (var row in Fields)
                row.ApplyRoiFromService(_roiState.GetRoi(row.RowId));
        }
    }

    private void OnRoiChanged(Guid rowId, RelativeRoi roi, RoiWriteSource source)
    {
        if (Debugger.IsAttached)
            Debug.WriteLine($"[ROI CHANGED EVENT] rowId={rowId}, source={source}, roi={roi}");
        var row = Fields.FirstOrDefault(r => r.RowId == rowId);
        row?.ApplyRoiFromService(roi);
    }

    public string WindowTitle =>
        _mode == TemplateEditorMode.Create
            ? Strings.UI.TemplateEditorTitleCreate
            : Strings.UI.TemplateEditorTitleEdit;

    public ObservableCollection<TemplateEditorFieldRowViewModel> Fields { get; }

    public RoiStateService RoiState => _roiState;

    public TemplateEditorFieldRowViewModel? SelectedField
    {
        get => _selectedField;
        set
        {
            if (Set(ref _selectedField, value))
            {
                SyncSelectionFlags();
                RaiseRemoveCommand();
            }
        }
    }

    public string EditorMessage
    {
        get => _editorMessage;
        private set => Set(ref _editorMessage, value);
    }

    /// <summary>Non-null when save completed successfully.</summary>
    public string? SavedTemplatePath { get; private set; }

    public event Action<bool>? CloseRequested;

    public ICommand AddFieldCommand { get; }
    public ICommand RemoveFieldCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnFieldsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isBatchUpdating)
            return;

        if (Debugger.IsAttached)
            Debug.WriteLine($"[COLLECTION CHANGED] action={e.Action}, newItems={e.NewItems?.Count}, oldItems={e.OldItems?.Count}");
            

        if (e.NewItems is not null)
        {
            foreach (TemplateEditorFieldRowViewModel row in e.NewItems)
            {
                TrackRowVmFromCollection(row);
                AttachRow(row);
            }
        }

        if (e.OldItems is not null)
        {
            foreach (TemplateEditorFieldRowViewModel row in e.OldItems)
            {
                if (Debugger.IsAttached)
                    Debug.WriteLine($"[DETACH ROW] rowId={row.RowId}, vmHash={row.GetHashCode()}");

                row.PropertyChanged -= OnRowPropertyChanged;
                row.RoiEditRequested -= OnRowRoiEditRequested;
            }
        }

        RaiseSaveCommand();
    }

    private void AttachRow(TemplateEditorFieldRowViewModel row)
    {
        if (Debugger.IsAttached)
        {
            Debug.WriteLine($"[ATTACH ROW] rowId={row.RowId}, vmHash={row.GetHashCode()}");
            Debug.WriteLine($"[ATTACH ROW ENTRY] rowId={row.RowId}, vmHash={row.GetHashCode()}");
        }

        if (_rowVmCache.TryGetValue(row.RowId, out var cached) && !ReferenceEquals(cached, row))
        {
            if (Debugger.IsAttached)
                Debug.WriteLine(
                    $"[ROW VM DUPLICATE INSTANCE DETECTED] rowId={row.RowId}, cachedHash={cached.GetHashCode()}, newHash={row.GetHashCode()}");
        }

        row.PropertyChanged -= OnRowPropertyChanged;
        row.PropertyChanged += OnRowPropertyChanged;

        row.RoiEditRequested -= OnRowRoiEditRequested;
        row.RoiEditRequested += OnRowRoiEditRequested;

        // 一次性初始化：禁止重复 Init(0,0,0,0) 覆盖已存在的真实 ROI（防回退）
        if (_initializedRows.Contains(row.RowId))
            return;

        _initializedRows.Add(row.RowId);

        _roiState.EnsureRow(row.RowId, new RelativeRoi(0, 0, 0, 0), RoiWriteSource.Init);
        row.ApplyRoiFromService(_roiState.GetRoi(row.RowId));
    }

    private void OnRowRoiEditRequested(Guid rowId, string propName, double value)
    {
        if (Debugger.IsAttached)
            Debug.WriteLine($"[ROI DATAGRID WRITE] rowId={rowId}, prop={propName}, value={value}");

        var row = Fields.FirstOrDefault(r => r.RowId == rowId);
        if (row is null)
            return;

        var cur = _roiState.GetRoi(rowId);

        var next = propName switch
        {
            nameof(TemplateEditorFieldRowViewModel.EditableRoiX) => cur with { X = value },
            nameof(TemplateEditorFieldRowViewModel.EditableRoiY) => cur with { Y = value },
            nameof(TemplateEditorFieldRowViewModel.EditableRoiW) => cur with { W = value },
            nameof(TemplateEditorFieldRowViewModel.EditableRoiH) => cur with { H = value },
            _ => cur,
        };

        _roiState.SetRoi(rowId, next, RoiWriteSource.DataGrid);
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TemplateEditorFieldRowViewModel.IsSn)
            && sender is TemplateEditorFieldRowViewModel row
            && row.IsSn)
        {
            foreach (var r in Fields)
            {
                if (!ReferenceEquals(r, row) && r.IsSn)
                    r.IsSn = false;
            }
        }

        EditorMessage = string.Empty;
        RaiseSaveCommand();
    }

    private void EnsureDefaultFirstRow()
    {
        if (Fields.Count > 0)
            return;
        var rowId = Guid.NewGuid();
        var row = GetOrCreateRowVM(rowId);
        row.Name = "SN";
        row.IsSn = true;
        Fields.Add(row);
    }

    private void TryLoadTemplate(string path)
    {
        try
        {
            var def = _loader.Load(path);
            ApplyTemplateDefinition(def);

            if (Fields.Count == 0)
                EnsureDefaultFirstRow();
            else
                EnsureSingleSnFlag();

            if (SelectedField is null && Fields.Count > 0)
                SelectedField = Fields[0];

            SyncSelectionFlags();
        }
        catch
        {
            EditorMessage = Strings.Messages.TemplateEditor.LoadFailed;
            EnsureDefaultFirstRow();
            SyncSelectionFlags();
        }
    }

    public void ApplyTemplateDefinitionForTest(TemplateDefinition def, bool overwriteRoi = false)
        => ApplyTemplateDefinition(def, overwriteRoi);

    private void ApplyTemplateDefinition(TemplateDefinition def, bool overwriteRoi = false)
    {
        ArgumentNullException.ThrowIfNull(def);

        using (_roiState.BeginInitialization())
        {
            _isBatchUpdating = true;
            try
            {
        // ✅ 不清空重建：按字段名复用现有 Row，避免 ROI 被默认值覆盖
        var existingByName = Fields.ToDictionary(r => NormalizeName(r), StringComparer.Ordinal);

        // 目标顺序：与模板一致
        var desired = new List<TemplateEditorFieldRowViewModel>(def.Fields.Count);

        foreach (var f in def.Fields)
        {
            var name = NormalizeName(f.Name ?? string.Empty, f.IsSn);

            if (!existingByName.TryGetValue(name, out var row))
            {
                // 仅对“新出现的字段”创建 Row；已有字段必须复用
                var rowId = Guid.NewGuid();
                row = GetOrCreateRowVM(rowId);
                row.Name = name;
                row.IsSn = f.IsSn;

                // 新行初始化 ROI（只允许 service 写）
                _roiState.EnsureRow(row.RowId, new RelativeRoi(0, 0, 0, 0), RoiWriteSource.TemplateLoad);
                row.ApplyRoiFromService(_roiState.GetRoi(row.RowId));
            }

            // ✅ IsSn 以模板为准（后续 EnsureSingleSnFlag 会再兜底）
            row.IsSn = f.IsSn;
            if (row.IsSn)
                row.Name = "SN";
            else
                row.Name = f.Name ?? string.Empty;

            // 模板加载默认不覆盖 ROI；只有 overwriteRoi=true 才允许写入（且 0x0 不写）
            if (overwriteRoi && !(f.Roi.W == 0 && f.Roi.H == 0))
                _roiState.SetRoi(row.RowId, f.Roi, RoiWriteSource.TemplateLoad);

            desired.Add(row);
        }

        // 将 Fields 调整为 desired（复用实例；只在必要时 Add/Remove）
        for (var i = 0; i < desired.Count; i++)
        {
            var target = desired[i];
            if (i < Fields.Count)
            {
                if (!ReferenceEquals(Fields[i], target))
                {
                    Fields.Remove(target); // 如果已经存在于后面位置，先移除再插入
                    Fields.Insert(i, target);
                }
            }
            else
            {
                Fields.Add(target);
            }
        }

        while (Fields.Count > desired.Count)
            Fields.RemoveAt(Fields.Count - 1);

                // batch 完成后统一 Attach，避免 CollectionChanged 风暴
                foreach (var row in Fields)
                    AttachRow(row);
            }
            finally
            {
                _isBatchUpdating = false;
            }
        }
    }

    private static string NormalizeName(TemplateEditorFieldRowViewModel row)
        => NormalizeName(row.Name ?? string.Empty, row.IsSn);

    private static string NormalizeName(string name, bool isSn)
        => isSn ? "SN" : name.Trim();

    private void EnsureSingleSnFlag()
    {
        var snRows = Fields.Where(r => r.IsSn).ToList();
        if (snRows.Count <= 1)
            return;
        for (var i = 1; i < snRows.Count; i++)
            snRows[i].IsSn = false;
    }

    private void AddField()
    {
        var rowId = Guid.NewGuid();
        var row = GetOrCreateRowVM(rowId);
        row.Name = string.Empty;
        row.IsSn = false;

        Fields.Add(row);
        _roiState.SetRoi(row.RowId, new RelativeRoi(0, 0, 0.5, 0.2), RoiWriteSource.Init);
        row.ApplyRoiFromService(_roiState.GetRoi(row.RowId));
        RaiseRemoveCommand();
    }

    private void RemoveField()
    {
        if (SelectedField is null)
            return;
        Fields.Remove(SelectedField);
        SelectedField = null;
        if (Fields.Count == 0)
            EnsureDefaultFirstRow();
        RaiseRemoveCommand();
        SyncSelectionFlags();
    }

    private void SyncSelectionFlags()
    {
        foreach (var row in Fields)
            row.IsSelected = ReferenceEquals(row, SelectedField);
    }

    public readonly record struct CanvasRect(double X, double Y, double W, double H);

    public CanvasRect GetCanvasRect(TemplateEditorFieldRowViewModel row, double canvasWidth, double canvasHeight)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0)
            return new CanvasRect(0, 0, 0, 0);

        return new CanvasRect(
            X: row.RoiX * canvasWidth,
            Y: row.RoiY * canvasHeight,
            W: row.RoiW * canvasWidth,
            H: row.RoiH * canvasHeight);
    }

    public void ApplyDragToSelectedPixels(double startX, double startY, double endX, double endY, double canvasWidth, double canvasHeight)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0)
            return;
        if (SelectedField is null)
            return;

        _roiState.UpdateRoi(
            SelectedField.RowId,
            new RoiRectPixels(
                Left: Math.Min(startX, endX),
                Top: Math.Min(startY, endY),
                Width: Math.Abs(endX - startX),
                Height: Math.Abs(endY - startY)),
            new CanvasSize(canvasWidth, canvasHeight),
            RoiWriteSource.Mouse);
    }

    public void MoveSelectedPixels(double deltaX, double deltaY, double canvasWidth, double canvasHeight)
    {
        if (SelectedField is null)
            return;
        if (canvasWidth <= 0 || canvasHeight <= 0)
            return;
        _roiState.MoveRoi(SelectedField.RowId, deltaX / canvasWidth, deltaY / canvasHeight, RoiWriteSource.MoveOperation);
    }

    private static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);
    private static double Clamp01(double v) => Clamp(v, 0, 1);

    private bool CanSave() => ValidateCore(out _);

    private bool ValidateCore(out string message)
    {
        message = string.Empty;
        if (Fields.Count == 0)
        {
            message = Strings.Messages.TemplateEditor.ValidationNoFields;
            return false;
        }

        if (!Fields.Any(f => f.IsSn))
        {
            message = Strings.Messages.TemplateEditor.ValidationNoSn;
            return false;
        }

        foreach (var row in Fields)
        {
            if (!row.IsSn && string.IsNullOrWhiteSpace(row.Name))
            {
                message = Strings.Messages.TemplateEditor.ValidationEmptyFieldName;
                return false;
            }

            var xs = new[] { row.RoiX, row.RoiY, row.RoiW, row.RoiH };
            if (xs.Any(v => v < 0 || v > 1))
            {
                message = Strings.Messages.TemplateEditor.ValidationRoiRange;
                return false;
            }
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in Fields)
        {
            var name = row.IsSn ? "SN" : row.Name.Trim();
            if (!names.Add(name))
            {
                message = Strings.Messages.TemplateEditor.ValidationDuplicateFieldName;
                return false;
            }
        }

        return true;
    }

    private void Save()
    {
        if (!ValidateCore(out var msg))
        {
            EditorMessage = msg;
            return;
        }

        if (!TryBuildDefinition(out var def) || def is null)
            return;

        var path = _dialogs.PickSaveTemplateFile();
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            _writer.Save(path, def);
            SavedTemplatePath = path;
            CloseRequested?.Invoke(true);
        }
        catch
        {
            EditorMessage = Strings.Messages.TemplateEditor.SaveFailed;
        }
    }

    private bool TryBuildDefinition(out TemplateDefinition? def)
    {
        def = null;
        var list = new List<TemplateFieldDefinition>(Fields.Count);
        foreach (var row in Fields)
        {
            var name = row.IsSn ? "SN" : row.Name.Trim();
            var roi = _roiState.GetRoi(row.RowId);
            list.Add(new TemplateFieldDefinition(
                name,
                roi,
                row.IsSn));
        }

        def = new TemplateDefinition(list);
        return true;
    }

    private void Cancel()
    {
        SavedTemplatePath = null;
        CloseRequested?.Invoke(false);
    }

    private void RaiseSaveCommand()
    {
        if (SaveCommand is RelayCommand s)
            s.RaiseCanExecuteChanged();
    }

    private void RaiseRemoveCommand()
    {
        if (RemoveFieldCommand is RelayCommand r)
            r.RaiseCanExecuteChanged();
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    public bool ValidateForTest() => ValidateCore(out _);

    public bool TryBuildDefinitionForTest(out TemplateDefinition? def) => TryBuildDefinition(out def);
}
