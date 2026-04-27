using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using CogniLabel.Core.Roi;

namespace CogniLabel.Presentation.ViewModels;

public sealed class TemplateEditorFieldRowViewModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private bool _isSn;
    private bool _isSelected;
    private double _roiX;
    private double _roiY;
    private double _roiW;
    private double _roiH;

    private bool _suppressEditableRoiChange;
    private double _editableRoiX;
    private double _editableRoiY;
    private double _editableRoiW;
    private double _editableRoiH;

    public TemplateEditorFieldRowViewModel() : this(Guid.NewGuid())
    {
    }

    public TemplateEditorFieldRowViewModel(Guid rowId)
    {
        RowId = rowId;
        if (Debugger.IsAttached)
            Debug.WriteLine($"[ROW VM CREATE] rowId={RowId}, hash={GetHashCode()}");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>用于在 RoiStateService 中索引 ROI 的唯一标识。</summary>
    public Guid RowId { get; }

    public event Action<Guid, string, double>? RoiEditRequested;

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public bool IsSn
    {
        get => _isSn;
        set => Set(ref _isSn, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    public double RoiX
    {
        get => _roiX;
        private set => Set(ref _roiX, value);
    }

    public double RoiY
    {
        get => _roiY;
        private set => Set(ref _roiY, value);
    }

    public double RoiW
    {
        get => _roiW;
        private set => Set(ref _roiW, value);
    }

    public double RoiH
    {
        get => _roiH;
        private set => Set(ref _roiH, value);
    }

    /// <summary>
    /// DataGrid 编辑入口（最终由 TemplateEditorViewModel 转发到 RoiStateService）。
    /// 注意：禁止直接写 RoiX/Y/W/H（只读展示值）。
    /// </summary>
    public double EditableRoiX
    {
        get => _editableRoiX;
        set => RequestRoiEdit(nameof(EditableRoiX), ref _editableRoiX, value);
    }

    public double EditableRoiY
    {
        get => _editableRoiY;
        set => RequestRoiEdit(nameof(EditableRoiY), ref _editableRoiY, value);
    }

    public double EditableRoiW
    {
        get => _editableRoiW;
        set => RequestRoiEdit(nameof(EditableRoiW), ref _editableRoiW, value);
    }

    public double EditableRoiH
    {
        get => _editableRoiH;
        set => RequestRoiEdit(nameof(EditableRoiH), ref _editableRoiH, value);
    }

    internal void ApplyRoiFromService(RelativeRoi roi)
    {
        if (Debugger.IsAttached)
        {
            Debug.WriteLine($"[ROI APPLY] row={Name}, rowId={RowId}, roi={roi}");
            Debug.WriteLine($"[ROW APPLY ENTRY] rowId={RowId}, vmHash={GetHashCode()}, roi={roi}");
        }

        RoiX = roi.X;
        RoiY = roi.Y;
        RoiW = roi.W;
        RoiH = roi.H;

        // 同步回 DataGrid 可编辑值（避免 UI 显示旧值），但禁止触发回写
        _suppressEditableRoiChange = true;
        try
        {
            Set(ref _editableRoiX, roi.X, nameof(EditableRoiX));
            Set(ref _editableRoiY, roi.Y, nameof(EditableRoiY));
            Set(ref _editableRoiW, roi.W, nameof(EditableRoiW));
            Set(ref _editableRoiH, roi.H, nameof(EditableRoiH));
        }
        finally
        {
            _suppressEditableRoiChange = false;
        }
    }

    private void RequestRoiEdit(string name, ref double field, double value)
    {
        if (_suppressEditableRoiChange)
        {
            Set(ref field, value, name);
            return;
        }

        if (!Set(ref field, value, name))
            return;

        // 由 VM 统一转发到 RoiStateService（唯一写入口）
        RoiEditRequested?.Invoke(RowId, name, value);
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        if (System.Diagnostics.Debugger.IsAttached && name is not null && name.Contains("Roi", StringComparison.Ordinal))
            System.Diagnostics.Debug.WriteLine($"[ROI CHANGE] {name}: {field} -> {value}");

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
