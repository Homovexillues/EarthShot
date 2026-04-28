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
    /// <summary>
    /// 用户选择区域的矩形框控件，初始不可见，在用户开始选择时显示，
    /// 并根据用户拖动调整位置和大小。
    /// </summary>
    private readonly Rectangle _selectionBox;

    /// <summary>
    /// 显示鼠标坐标和选区信息的文本控件，始终可见，实时更新内容。
    /// </summary>
    private readonly TextBlock _cursorLabel;

    /// <summary>
    /// 背景图像控件，用于显示屏幕快照。
    /// </summary>
    private readonly Image _backdrop;

    /// <summary>
    /// 围绕选区的 4 个裙边遮罩，用于"挖洞"高亮效果——选区内部透明，外部半透明压暗。
    /// </summary>
    private readonly Rectangle _maskTop;
    private readonly Rectangle _maskBottom;
    private readonly Rectangle _maskLeft;
    private readonly Rectangle _maskRight;

    /// <summary>
    /// 屏幕快照的像素数据，供用户选择区域时裁剪用。注意它是物理像素尺寸，坐标系与鼠标事件一致（都是设备无关像素），不需要考虑 DPI 缩放。
    /// </summary>
    private readonly CapturedImage _snapshot;

    /// <summary>
    /// 用户选择区域的状态机：
    /// Idle（未开始选择）
    /// Selecting（正在选择）
    /// Selected（已选择）
    /// 注意 Selected 状态下用户还可以继续调整选区，直到按下 Esc 键或右键取消。
    /// </summary>
    private SelectionState _state = SelectionState.Idle;

    /// <summary>
    /// 用户选择区域的起点坐标，都是设备无关像素（DIP），与鼠标事件坐标系一致。选区矩形由它们计算得出。
    /// </summary>
    private Point _start;

    /// <summary>
    /// 用户选择区域的当前坐标，都是设备无关像素（DIP），与鼠标事件坐标系一致。选区矩形由它们计算得出。
    /// </summary>
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
        _maskTop = this.FindControl<Rectangle>("MaskTop")!;
        _maskBottom = this.FindControl<Rectangle>("MaskBottom")!;
        _maskLeft = this.FindControl<Rectangle>("MaskLeft")!;
        _maskRight = this.FindControl<Rectangle>("MaskRight")!;
        _snapshot = snapshot;
        if (!_snapshot.IsEmpty)
            _backdrop.Source = ToBitmap(_snapshot);
    }

    /// <summary>
    /// 窗口完成布局并显示后调用，此时 Bounds 才是有效尺寸——在这里给遮罩铺满全屏，
    /// 让用户一打开就看到压暗效果。
    /// </summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        UpdateSelectionBox();
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
                $"Selecting: {rect.X:F0}, {rect.Y:F0}  {rect.Width:F0} x {rect.Height:F0}";
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
        _cursorLabel.Text = $"Selected: {image.Width} x {image.Height} {image.Pixels.Length} bytes";
        Console.WriteLine($"Selected: {image.Width} x {image.Height} {image.Pixels.Length} bytes");
        var px = image.Pixels.Span;
        if (!image.IsEmpty)
        {
            int i = (image.Height / 2) * image.Stride + (image.Width / 2) * 4;
            Console.WriteLine($"  center BGRA = {px[i]},{px[i + 1]},{px[i + 2]},{px[i + 3]}");
        }
    }

    /// <summary>
    /// 同步更新选区矩形和 4 个裙边遮罩。空选区（Idle 初始或刚按下未拖动）时
    /// 4 个裙边自然组合成全屏遮罩；有选区时围出"挖洞"效果。
    /// </summary>
    private void UpdateSelectionBox()
    {
        var rect = Normalize(_start, _current);
        Canvas.SetLeft(_selectionBox, rect.X);
        Canvas.SetTop(_selectionBox, rect.Y);
        _selectionBox.Width = rect.Width;
        _selectionBox.Height = rect.Height;

        // 用窗口当前尺寸作为遮罩外边界。FullScreen 下 Bounds == 屏幕 DIP 尺寸。
        var bw = Bounds.Width;
        var bh = Bounds.Height;
        // 选区可能被拖出窗口（Pointer.Capture 之后），裁到窗口内才能正确分割
        var x = Math.Clamp(rect.X, 0, bw);
        var y = Math.Clamp(rect.Y, 0, bh);
        var r = Math.Clamp(rect.Right, 0, bw);
        var b = Math.Clamp(rect.Bottom, 0, bh);

        // Top: 选区上方的整条
        Canvas.SetLeft(_maskTop, 0);
        Canvas.SetTop(_maskTop, 0);
        _maskTop.Width = bw;
        _maskTop.Height = y;

        // Bottom: 选区下方的整条
        Canvas.SetLeft(_maskBottom, 0);
        Canvas.SetTop(_maskBottom, b);
        _maskBottom.Width = bw;
        _maskBottom.Height = Math.Max(0, bh - b);

        // Left: 选区左侧（夹在 top/bottom 之间）
        Canvas.SetLeft(_maskLeft, 0);
        Canvas.SetTop(_maskLeft, y);
        _maskLeft.Width = x;
        _maskLeft.Height = Math.Max(0, b - y);

        // Right: 选区右侧（夹在 top/bottom 之间）
        Canvas.SetLeft(_maskRight, r);
        Canvas.SetTop(_maskRight, y);
        _maskRight.Width = Math.Max(0, bw - r);
        _maskRight.Height = Math.Max(0, b - y);
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
        // Windows的标准DPI，每英寸96像素点
        var dpi = new Vector(96, 96);
        var format =
            image.Format == CorePixelFormat.Bgra8888
                ? AvaloniaPixelFormat.Bgra8888
                : AvaloniaPixelFormat.Rgba8888;
        // AlphaFormat,即ARGB里A的含义。
        // Premultiplied表示预乘，Opaque表示不透明，Straight表示非预乘。
        // 屏幕截图是完全不透明的，所以选 Opaque，避免不必要的预乘运算。
        var wb = new WriteableBitmap(size, dpi, format, AlphaFormat.Opaque);
        // 锁定位图内存，获取指针，逐行复制像素数据到位图内存中
        using var fb = wb.Lock();
        var src = image.Pixels.Span;
        // Avalonia 的 RowBytes 可能与我们的 Stride 不等（极少见但要防）——按行复制
        var rowBytes = image.Width * 4;
        for (int y = 0; y < image.Height; y++)
        {
            // 从源图像的第 y 行复制 rowBytes 字节到目标位图的第 y 行
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
