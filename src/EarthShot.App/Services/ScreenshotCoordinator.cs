using Avalonia.Threading;
using EarthShot.App.Views;
using EarthShot.Core.Capturing;
using EarthShot.Core.Capturing.Windows;

namespace EarthShot.App.Services;

public sealed class ScreenshotCoordinator
{
    private readonly IScreenCapturer _capturer = new GdiScreenCapturer();
    private OverlayWindow? _active;

    /// 触发一次截图会话。可以从任意线程调用——会自动 marshal 到 UI 线程。
    public void RequestSession()
    {
        // Avalonia的窗口操作必须在UI线程进行，所以这里做个线程检查和切换
        if (Dispatcher.UIThread.CheckAccess())
        {
            RequestSessionCore();
        }
        else
        {
            Dispatcher.UIThread.Post(RequestSessionCore);
        }
    }

    private void RequestSessionCore()
    {
        // 重入保护:已有会话就丢弃
        if (_active is not null)
        {
            return;
        }
        var snapshot = _capturer.Capture(_capturer.PrimaryScreenBounds);
        var window = new OverlayWindow(snapshot);
        window.Closed += (_, _) => _active = null; // 关窗时清状态
        _active = window;
        window.Show();
    }
}
