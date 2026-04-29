using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using EarthShot.App.Actions;
using EarthShot.Core.Capturing;
using EarthShot.Core.Pipeline;
using EarthShot.Core.Processing;
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
    /// 负责处理用户选择的图像并返回结果的处理器实例。
    /// </summary>
    private readonly ScreenshotPipeline _screenshotPipeline;

    /// <summary>
    /// Toast 容器（控制显示/隐藏），里面是文字。
    /// 显示性挂在 Border 上而不是 TextBlock 上——父级 IsVisible=False 时子级不会渲染。
    /// </summary>
    private readonly Border _toastBorder;
    private readonly TextBlock _toastText;

    /// <summary>
    /// Toast 消息显示的持续时间。
    /// </summary>
    private readonly TimeSpan _toastDuration = TimeSpan.FromSeconds(2);

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
    /// 用户选择区域的状态机。详见 SelectionState 枚举注释。
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
        _snapshot = snapshot;
        _backdrop = this.FindControl<Image>("Backdrop")!;
        _cursorLabel = this.FindControl<TextBlock>("CursorLabel")!;
        _maskTop = this.FindControl<Rectangle>("MaskTop")!;
        _maskBottom = this.FindControl<Rectangle>("MaskBottom")!;
        _maskLeft = this.FindControl<Rectangle>("MaskLeft")!;
        _maskRight = this.FindControl<Rectangle>("MaskRight")!;
        _selectionBox = this.FindControl<Rectangle>("SelectionBox")!;
        _toastBorder = this.FindControl<Border>("Toast")!;
        _toastText = this.FindControl<TextBlock>("ToastText")!;
        _screenshotPipeline = new ScreenshotPipeline(
            [new QRCodeProcessor()],
            [new ClipboardAction(() => TopLevel.GetTopLevel(this)?.Clipboard)]
        );
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
        // 右键退出
        if (props.IsRightButtonPressed)
        {
            Close();
            return;
        }
        // 左键没点退出
        if (!props.IsLeftButtonPressed)
            return;
        // 有图正在处理退出
        if (_state == SelectionState.Processing)
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

    protected override async void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_state != SelectionState.Selecting)
            return;

        e.Pointer.Capture(null);
        _state = SelectionState.Processing;
        UpdateSelectionBox();

        var rect = Normalize(_start, _current);
        var scaling = this.RenderScaling;
        var physicalRect = new PixelRect(
            (int)(rect.X * scaling),
            (int)(rect.Y * scaling),
            (int)(rect.Width * scaling),
            (int)(rect.Height * scaling)
        );
        // 不再调 GDI——直接从快照内存裁剪（同步、毫秒级）
        var image = _snapshot.Crop(physicalRect);
        // 不要外层包 Task.Run——Pipeline 自己会把 CPU 工作分到后台线程，
        // action 留在 UI 线程跑，这样 ClipboardAction 才能合法访问 this.Clipboard
        var results = await _screenshotPipeline.RunAsync(image);
        Console.WriteLine($"[release] pipeline returned {results.Count} results");
        var sb = new System.Text.StringBuilder();
        foreach (var r in results)
        {
            if (r is QrCodeResult qr)
            {
                sb.Append($"QR Code: {qr.Text}");
            }
        }
        var text = sb.Length == 0 ? "No QR code found" : sb.ToString();
        await ShowToastThenCloseAsync(text);
    }

    /// <summary>
    /// 显示Toast消息，持续一段时间后自动关闭窗口。Toast是覆盖在界面上的临时消息提示，不会打断用户操作。
    /// </summary>
    /// <param name="text">要显示的消息文本</param>
    /// <returns></returns>
    private async Task ShowToastThenCloseAsync(string text)
    {
        _toastText.Text = text;
        _toastBorder.IsVisible = true;
        Topmost = true;
        await Task.Delay(_toastDuration);
        Close();
    }

    /// <summary>
    /// Avalonia 写剪贴板走的是 OLE 路径（OleSetClipboard），数据是"延迟渲染"——
    /// 实际字节由我们进程的 IDataObject 按需提供。一旦进程退出，IDataObject 析构，
    /// 系统就把剪贴板清空了。
    ///
    /// OleFlushClipboard 让 OLE 立刻把数据序列化到全局内存，独立于我们的进程，
    /// 这样关窗后用户 Ctrl+V 还能粘到。Windows 专属——别的平台这步不需要也无效。
    /// </summary>
    // LibraryImport 走 source generator，AOT 友好；返回 HRESULT 由调用方判断
    [LibraryImport("ole32.dll")]
    private static partial int OleFlushClipboard();

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (!OperatingSystem.IsWindows())
            return;
        try
        {
            var hr = OleFlushClipboard();
            if (hr != 0)
                Console.Error.WriteLine($"[OleFlushClipboard] HRESULT 0x{hr:X8}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[OleFlushClipboard] {ex.Message}");
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

    /// <summary>
    /// 将两个对角点坐标规范化成一个矩形（左上角坐标 + 宽高）。
    /// 用户可能从任意方向拖动选区，所以要 Math.Min/Max 一下。
    /// 坐标系都是设备无关像素（DIP），与鼠标事件一致，不需要考虑 DPI 缩放。
    /// </summary>
    /// <param name="a">第一个对角点</param>
    /// <param name="b">第二个对角点</param>
    /// <returns>规范化后的矩形</returns>
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

/// <summary>
/// 用户选择区域的状态机：
/// Idle（未开始选择）
/// Selecting（正在拖拽选区）
/// Processing（已松开，正在解码 + 显示 toast + 等待关窗——此状态下忽略新的左键，
///             避免异步尾巴里的多个 ShowToastThenCloseAsync 互相覆盖文本/重复 Close）
/// 任何状态下 Esc / 右键都会立即关窗。
/// </summary>
internal enum SelectionState
{
    Idle,
    Selecting,
    Processing,
}
