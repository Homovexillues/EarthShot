using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EarthShot.App.Services;

namespace EarthShot.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    private ScreenshotCoordinator? _coordinator;
    private HotKeyService? _hotKeyService;

    public void OnTrayCapture(object sender, EventArgs e)
    {
        if (_coordinator is null)
        {
            throw new InvalidOperationException($"{nameof(_coordinator)} is not initialized yet.");
        }
        _coordinator.RequestSession();
    }

    public void OnTrayExit(object sender, EventArgs e)
    {
        // 处理托盘图标的退出事件
        // 清理热键监听线程
        // 例如，关闭应用程序
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 只在显示调用 Shutdown() 方法时才关闭应用程序
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            // 初始化截图协调器
            _coordinator = new ScreenshotCoordinator();
            _hotKeyService = new HotKeyService(_coordinator.RequestSession);
            _hotKeyService.Start();
            // 确保即使是不在托盘图标上点退出也能正常退出
            desktop.Exit += (_, _) => _hotKeyService.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
