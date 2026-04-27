using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using EarthShot.Core.Capturing;
using AvaloniaPixelFormat = Avalonia.Platform.PixelFormat;
using CorePixelFormat = EarthShot.Core.Capturing.PixelFormat;
using PixelRect = EarthShot.Core.Capturing.PixelRect;

namespace EarthShot.App.Views;

public partial class OverlayWindow : Window
{
    private readonly Rectangle _selectionBox;
    private readonly TextBlock _cursorLabel;
    private readonly Image _backdrop;
    private readonly CapturedImage _snapshot;

    private SelectionState _state = SelectionState.Idle;
    private Point _start;
    private Point _current;

    // 仅供 Avalonia XAML loader / 设计器使用，运行时一律走带快照的构造器
    public OverlayWindow()
        : this(default) { }

    public OverlayWindow(CapturedImage snapshot)
    {
        InitializeComponent();
        _selectionBox = this.FindControl<Rectangle>("SelectionBox")!;
        _cursorLabel = this.FindControl<TextBlock>("CursorLabel")!;
        _backdrop = this.FindControl<Image>("Backdrop")!;
        _snapshot = snapshot;
        if (!_snapshot.IsEmpty)
            _backdrop.Source = ToBitmap(_snapshot);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
            Close();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsRightButtonPressed)
        {
            Close();
            return;
        }

        if (!props.IsLeftButtonPressed)
            return;

        _start = e.GetPosition(this);
        _current = _start;
        _state = SelectionState.Selecting;
        _selectionBox.IsVisible = true;
        UpdateSelectionBox();
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _current = e.GetPosition(this);

        if (_state == SelectionState.Selecting)
        {
            UpdateSelectionBox();
            var rect = Normalize(_start, _current);
            _cursorLabel.Text =
                $"Selecting: {rect.X:F0}, {rect.Y:F0}  {rect.Width:F0} × {rect.Height:F0}";
        }
        else
        {
            _cursorLabel.Text = $"Cursor: {_current.X:F0}, {_current.Y:F0}";
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_state != SelectionState.Selecting)
            return;

        e.Pointer.Capture(null);
        _state = SelectionState.Selected;
        UpdateSelectionBox();

        var rect = Normalize(_start, _current);
        var scaling = this.RenderScaling;
        var physicalRect = new PixelRect(
            (int)(rect.X * scaling),
            (int)(rect.Y * scaling),
            (int)(rect.Width * scaling),
            (int)(rect.Height * scaling)
        );
        // 不再调 GDI——直接从快照内存裁剪
        var image = _snapshot.Crop(physicalRect);
        _cursorLabel.Text =
            $"Selected: {image.Width} × {image.Height} {image.Pixels.Length} bytes";
        Console.WriteLine(
            $"Selected: {image.Width} × {image.Height} {image.Pixels.Length} bytes"
        );
    }

    private void UpdateSelectionBox()
    {
        var rect = Normalize(_start, _current);
        Canvas.SetLeft(_selectionBox, rect.X);
        Canvas.SetTop(_selectionBox, rect.Y);
        _selectionBox.Width = rect.Width;
        _selectionBox.Height = rect.Height;
    }

    private static Rect Normalize(Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var w = Math.Abs(a.X - b.X);
        var h = Math.Abs(a.Y - b.Y);
        return new Rect(x, y, w, h);
    }

    /// <summary>
    /// 把内存里的 BGRA 字节流包装成 Avalonia 可绘制的 WriteableBitmap。
    /// 用 Stretch=Fill 时 dpi 字段不影响显示，填 96 即可。
    /// </summary>
    private static unsafe WriteableBitmap ToBitmap(CapturedImage image)
    {
        var size = new PixelSize(image.Width, image.Height);
        var dpi = new Vector(96, 96);
        var format =
            image.Format == CorePixelFormat.Bgra8888
                ? AvaloniaPixelFormat.Bgra8888
                : AvaloniaPixelFormat.Rgba8888;

        var wb = new WriteableBitmap(size, dpi, format, AlphaFormat.Opaque);

        using var fb = wb.Lock();
        var src = image.Pixels.Span;
        // Avalonia 的 RowBytes 可能与我们的 Stride 不等（极少见但要防）——按行复制
        var rowBytes = image.Width * 4;
        for (int y = 0; y < image.Height; y++)
        {
            var srcRow = src.Slice(y * image.Stride, rowBytes);
            var dstSpan = new Span<byte>((byte*)fb.Address + y * fb.RowBytes, rowBytes);
            srcRow.CopyTo(dstSpan);
        }
        return wb;
    }
}

internal enum SelectionState
{
    Idle,
    Selecting,
    Selected,
}
