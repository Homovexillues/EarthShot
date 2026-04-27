using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EarthShot.App.Views;
using EarthShot.Core.Capturing;
using EarthShot.Core.Capturing.Windows;

namespace EarthShot.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 此刻 OverlayWindow 还没创建，桌面是干净的——先截一次全屏快照
            IScreenCapturer capturer = new GdiScreenCapturer();
            var snapshot = capturer.Capture(capturer.PrimaryScreenBounds);
            desktop.MainWindow = new OverlayWindow(snapshot);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
