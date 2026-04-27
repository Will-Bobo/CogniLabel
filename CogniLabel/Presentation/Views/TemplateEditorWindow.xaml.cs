using CogniLabel.Presentation.ViewModels;
using CogniLabel.Application;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace CogniLabel.Presentation.Views;

public partial class TemplateEditorWindow : Window
{
    private const bool ROI_DEBUG = true;
    private BitmapSource? _bitmap;

    private bool _isCreating;
    private System.Windows.Point _createStart;
    private System.Windows.Point _createLast; // FIX: last valid point during drag
    private System.Windows.Shapes.Rectangle? _createRect;

    private bool _isMoving;
    private TemplateEditorFieldRowViewModel? _movingRow;
    private System.Windows.Point _moveLast;

    public TemplateEditorWindow(TemplateEditorViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested += ok =>
        {
            DialogResult = ok;
            Close();
        };
    }

    private TemplateEditorViewModel? Vm => DataContext as TemplateEditorViewModel;

    private bool IsOverlaySizeValid()
        => Overlay.ActualWidth > 0 && Overlay.ActualHeight > 0;

    private System.Windows.Point GetRootPoint(System.Windows.Input.MouseEventArgs e)
        => e.GetPosition(ImageHost); // FIX: unify to root canvas coordinates

    private System.Windows.Point GetRootPoint(System.Windows.Input.MouseButtonEventArgs e)
        => e.GetPosition(ImageHost); // FIX: unify to root canvas coordinates

    private void OnImageHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateLayoutForImage();
    }

    private void OnOverlaySizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ROI_DEBUG && System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[OVERLAY SIZE CHANGED] old={e.PreviousSize.Width}x{e.PreviousSize.Height} new={e.NewSize.Width}x{e.NewSize.Height}");
        }
    }

    private void OnLoadSampleImageClick(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "图片 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
            CheckFileExists = true,
        };
        if (dlg.ShowDialog() != true)
            return;

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(dlg.FileName);
            bmp.EndInit();
            bmp.Freeze();

            _bitmap = bmp;
            SampleImage.Source = bmp;
            UpdateLayoutForImage();
        }
        catch
        {
            // ignore in MVP
        }
    }

    private void UpdateLayoutForImage()
    {
        if (_bitmap is null || SampleImage.Source is null)
        {
            Overlay.Width = 0;
            Overlay.Height = 0;
            return;
        }

        // Stretch=Fill：Image 与 Overlay 使用同一坐标系（0,0 起点），避免 Uniform 带来的偏移与漂移
        var hostW = ImageHost.ActualWidth;
        var hostH = ImageHost.ActualHeight;
        if (hostW <= 0 || hostH <= 0)
            return;

        SampleImage.Width = hostW;
        SampleImage.Height = hostH;
        Canvas.SetLeft(SampleImage, 0);
        Canvas.SetTop(SampleImage, 0);

        Overlay.Width = hostW;
        Overlay.Height = hostH;
        Canvas.SetLeft(Overlay, 0);
        Canvas.SetTop(Overlay, 0);
    }

    private void OnOverlayMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isMoving)
            return;

        if (Vm?.SelectedField is null)
            return;

        if (!IsOverlaySizeValid())
            return;

        _isCreating = true;
        _createStart = GetRootPoint(e); // FIX: root coordinate system
        _createLast = _createStart; // FIX: init last point
        Overlay.CaptureMouse();

        _createRect = new System.Windows.Shapes.Rectangle
        {
            Stroke = System.Windows.Media.Brushes.Red,
            StrokeThickness = 1,
            Fill = System.Windows.Media.Brushes.Transparent,
            IsHitTestVisible = false,
        };
        Overlay.Children.Add(_createRect);
        Canvas.SetLeft(_createRect, _createStart.X);
        Canvas.SetTop(_createRect, _createStart.Y);
        _createRect.Width = 0;
        _createRect.Height = 0;
    }

    private void OnOverlayMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isCreating && _createRect is not null)
        {
            var p = GetRootPoint(e); // FIX: root coordinate system
            _createLast = p; // FIX: always keep last valid point
            var x1 = Math.Min(_createStart.X, p.X);
            var y1 = Math.Min(_createStart.Y, p.Y);
            var x2 = Math.Max(_createStart.X, p.X);
            var y2 = Math.Max(_createStart.Y, p.Y);

            Canvas.SetLeft(_createRect, x1);
            Canvas.SetTop(_createRect, y1);
            _createRect.Width = Math.Max(0, x2 - x1);
            _createRect.Height = Math.Max(0, y2 - y1);
        }
        else if (_isMoving && _movingRow is not null)
        {
            var p = GetRootPoint(e); // FIX: root coordinate system
            var dx = p.X - _moveLast.X;
            var dy = p.Y - _moveLast.Y;
            _moveLast = p;

            if (Overlay.ActualWidth > 0 && Overlay.ActualHeight > 0)
            {
                Vm?.RoiState.MoveRoi(
                    _movingRow.RowId,
                    dx / Overlay.ActualWidth,
                    dy / Overlay.ActualHeight,
                    RoiWriteSource.MoveOperation);
            }
        }
    }

    private void OnOverlayMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isCreating)
        {
            _isCreating = false;
            // FIX: do not refresh canvas size here; rely on Overlay/VM synced earlier (SizeChanged)

            // FIX: fast click / no-drag should not write ROI
            if (_createRect is not null && (_createRect.Width < 2 || _createRect.Height < 2))
            {
                // skip write-back
            }
            else
            {
                // FIX (core): use rect geometry instead of MouseUp position
                if (_createRect is not null)
                {
                    var left = Canvas.GetLeft(_createRect);
                    var top = Canvas.GetTop(_createRect);
                    var right = left + _createRect.Width;
                    var bottom = top + _createRect.Height;

                    if (ROI_DEBUG && System.Diagnostics.Debugger.IsAttached)
                    {
                        System.Diagnostics.Debug.WriteLine("==== UI MOUSE UP ====");
                        System.Diagnostics.Debug.WriteLine($"Rect: L={left}, T={top}, W={_createRect.Width}, H={_createRect.Height}");
                        System.Diagnostics.Debug.WriteLine($"Overlay: {Overlay.ActualWidth} x {Overlay.ActualHeight}");
                    }

                    if (Vm?.SelectedField is not null)
                    {
                        if (System.Diagnostics.Debugger.IsAttached)
                            System.Diagnostics.Debug.WriteLine($"[ROI MOUSE WRITE] rect={left},{top},{_createRect.Width},{_createRect.Height}, canvas={Overlay.ActualWidth}x{Overlay.ActualHeight}");

                        Vm.RoiState.UpdateRoi(
                            Vm.SelectedField.RowId,
                            new RoiRectPixels(left, top, _createRect.Width, _createRect.Height),
                            new CanvasSize(Overlay.ActualWidth, Overlay.ActualHeight),
                            RoiWriteSource.Mouse);
                    }
                }
            }

            // FIX: release capture after we have our end point
            Overlay.ReleaseMouseCapture();

            if (_createRect is not null)
            {
                Overlay.Children.Remove(_createRect);
                _createRect = null;
            }
        }

        if (_isMoving)
        {
            _isMoving = false;
            _movingRow = null;
            Overlay.ReleaseMouseCapture();
        }
    }

    private void OnRoiMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not TemplateEditorFieldRowViewModel row)
            return;

        if (Vm is not null)
            Vm.SelectedField = row;

        if (!IsOverlaySizeValid())
            return;

        _isMoving = true;
        _movingRow = row;
        _moveLast = GetRootPoint(e); // FIX: root coordinate system
        Overlay.CaptureMouse();
        e.Handled = true;
    }
}
