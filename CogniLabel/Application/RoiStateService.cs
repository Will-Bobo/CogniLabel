using CogniLabel.Core.Roi;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CogniLabel.Application;

public enum RoiWriteSource
{
    Unknown = 0,
    Mouse,
    TemplateLoad,
    DataGrid,
    TestInject,
    MoveOperation,
    Init,
}

public readonly record struct CanvasSize(double Width, double Height);
public readonly record struct RoiRectPixels(double Left, double Top, double Width, double Height);

/// <summary>
/// ROI 单一可信数据源（Single Source of Truth）。
/// Application 层唯一允许修改 ROI 的入口。
/// </summary>
public sealed class RoiStateService
{
    private static long _roiSeq;
    private readonly Dictionary<Guid, RelativeRoi> _roiByRowId = new();
    private readonly Dictionary<Guid, string> _lastSourceByRowId = new();
    private int _initDepth;

    public event Action<Guid, RelativeRoi, RoiWriteSource>? RoiChanged;

    public bool IsInitializing => _initDepth > 0;

    public IDisposable BeginInitialization()
    {
        _initDepth++;
        return new InitScope(this);
    }

    private sealed class InitScope : IDisposable
    {
        private RoiStateService? _owner;

        public InitScope(RoiStateService owner) => _owner = owner;

        public void Dispose()
        {
            var o = _owner;
            if (o is null)
                return;
            _owner = null;
            o._initDepth = Math.Max(0, o._initDepth - 1);
        }
    }

    public RelativeRoi GetRoi(Guid rowId)
        => _roiByRowId.TryGetValue(rowId, out var roi) ? roi : new RelativeRoi(0, 0, 0, 0);

    public void EnsureRow(Guid rowId, RelativeRoi initialRoi, RoiWriteSource source = RoiWriteSource.Init, [CallerMemberName] string? caller = null)
    {
        if (_roiByRowId.ContainsKey(rowId))
            return;

        if (Debugger.IsAttached)
        {
            Debug.WriteLine($"[ROI INIT] EnsureRow rowId={rowId}, source={source}, caller={caller}");
            Debug.WriteLine($"[ENSURE ROW CALL] rowId={rowId}, source={source}");
            Debug.WriteLine(Environment.StackTrace);
        }

        // 初始化写入：只落 state，不广播，不打日志（避免 0->0 噪音）
        var normalized = NormalizeAndClamp(initialRoi);
        _roiByRowId[rowId] = normalized;
        _lastSourceByRowId[rowId] = source.ToString();
    }

    public void SetRoi(Guid rowId, RelativeRoi roi)
        => SetRoi(rowId, roi, RoiWriteSource.Unknown, caller: null);

    public void SetRoi(Guid rowId, RelativeRoi roi, RoiWriteSource source, [CallerMemberName] string? caller = null)
    {
        var traceId = Interlocked.Increment(ref _roiSeq);
        roi = NormalizeAndClamp(roi);

        var hadOld = _roiByRowId.TryGetValue(rowId, out var old);

        if (Debugger.IsAttached)
        {
            _lastSourceByRowId.TryGetValue(rowId, out var lastSource);
            Debug.WriteLine($"[ROI WRITE #{traceId}] source={source}, caller={caller}, rowId={rowId}, hadOld={hadOld}, lastSource={lastSource}, old={old}, roi={roi}");
        }

        if (roi.X == 0 && roi.Y == 0 && roi.W == 0 && roi.H == 0 && source != RoiWriteSource.Init)
        {
            if (Debugger.IsAttached)
                Debug.WriteLine($"[ROI WARNING #{traceId}] ZERO ROI WRITE DETECTED! source={source}, caller={caller}, rowId={rowId}");
        }

        if (hadOld && old == roi)
            return; // 去噪：完全相同则禁止任何 event/log

        _roiByRowId[rowId] = roi;
        _lastSourceByRowId[rowId] = source.ToString();

        if (ShouldBroadcast(source))
        {
            DebugWrite(source, caller, traceId, rowId, roi);
            RoiChanged?.Invoke(rowId, roi, source);
        }
    }

    /// <summary>
    /// dx/dy 为相对位移（归一化：相对于 canvas 的比例）。
    /// </summary>
    public void MoveRoi(Guid rowId, double dx, double dy)
        => MoveRoi(rowId, dx, dy, RoiWriteSource.Unknown, caller: null);

    public void MoveRoi(Guid rowId, double dx, double dy, RoiWriteSource source, [CallerMemberName] string? caller = null)
    {
        var traceId = Interlocked.Increment(ref _roiSeq);
        var cur = GetRoi(rowId);

        if (Debugger.IsAttached)
            Debug.WriteLine($"[ROI MOVE #{traceId}] source={source}, caller={caller}, rowId={rowId}, dx={dx}, dy={dy}, cur={cur}");

        var nx = Clamp01(cur.X + dx);
        var ny = Clamp01(cur.Y + dy);

        // keep inside 0..1 while preserving size
        nx = Math.Min(nx, 1 - cur.W);
        ny = Math.Min(ny, 1 - cur.H);
        nx = Clamp01(nx);
        ny = Clamp01(ny);

        SetRoi(rowId, cur with { X = nx, Y = ny }, source, caller);
    }

    public void UpdateRoi(Guid rowId, RoiRectPixels roiRectPixels, CanvasSize canvasSize)
        => UpdateRoi(rowId, roiRectPixels, canvasSize, RoiWriteSource.Unknown, caller: null);

    public void UpdateRoi(Guid rowId, RoiRectPixels roiRectPixels, CanvasSize canvasSize, RoiWriteSource source, [CallerMemberName] string? caller = null)
    {
        var traceId = Interlocked.Increment(ref _roiSeq);
        if (canvasSize.Width <= 0 || canvasSize.Height <= 0)
            return;

        if (Debugger.IsAttached)
            Debug.WriteLine($"[ROI UPDATE #{traceId}] source={source}, caller={caller}, rowId={rowId}, rect=({roiRectPixels.Left},{roiRectPixels.Top},{roiRectPixels.Width},{roiRectPixels.Height}), canvas=({canvasSize.Width}x{canvasSize.Height})");

        var x1 = Clamp(roiRectPixels.Left, 0, canvasSize.Width);
        var y1 = Clamp(roiRectPixels.Top, 0, canvasSize.Height);
        var x2 = Clamp(roiRectPixels.Left + roiRectPixels.Width, 0, canvasSize.Width);
        var y2 = Clamp(roiRectPixels.Top + roiRectPixels.Height, 0, canvasSize.Height);

        var w = Math.Max(0, x2 - x1);
        var h = Math.Max(0, y2 - y1);

        var roi = new RelativeRoi(
            X: Clamp01(x1 / canvasSize.Width),
            Y: Clamp01(y1 / canvasSize.Height),
            W: Clamp01(w / canvasSize.Width),
            H: Clamp01(h / canvasSize.Height));

        if (Debugger.IsAttached && source == RoiWriteSource.Mouse)
            Debug.WriteLine($"[ROI MOUSE FINAL WRITE] rowId={rowId}, roi={roi}");

        SetRoi(rowId, roi, source, caller);
    }

    private static RelativeRoi NormalizeAndClamp(RelativeRoi roi)
    {
        var x = Clamp01(roi.X);
        var y = Clamp01(roi.Y);
        var w = Clamp01(roi.W);
        var h = Clamp01(roi.H);

        x = Math.Min(x, 1 - w);
        y = Math.Min(y, 1 - h);
        x = Clamp01(x);
        y = Clamp01(y);

        return new RelativeRoi(x, y, w, h);
    }

    private static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);
    private static double Clamp01(double v) => Clamp(v, 0, 1);

    private static void DebugWrite(RoiWriteSource source, string? caller, long traceId, Guid rowId, RelativeRoi roi)
        => Debug.WriteLine($"[ROI BROADCAST #{traceId}] source={source}, caller={caller}, rowId={rowId}, roi={roi}");

    private bool ShouldBroadcast(RoiWriteSource source)
    {
        // 初始化静默：允许写 state，但不广播 UI、不打印日志
        if (IsInitializing)
            return false;

        // source 分级：Init 永不广播；Mouse/DataGrid/Move 强广播；TemplateLoad 可选广播（默认不广播）
        return source is RoiWriteSource.Mouse
            or RoiWriteSource.DataGrid
            or RoiWriteSource.MoveOperation
            or RoiWriteSource.TestInject;
    }
}

