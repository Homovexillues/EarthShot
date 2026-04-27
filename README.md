# EarthShot

一个轻量的 Avalonia 截图工具，专注于框选即识别 QR 码。灵感来自 Flameshot，但只做一件事。

详细设计见 [设计文档.md](./设计文档.md)。

## 运行

```bash
dotnet run --project src/EarthShot.App
```

按 **Esc** 或鼠标右键退出覆盖窗口。

## 当前进度

- [x] **M1 骨架搭起** —— 空白 Avalonia + 全屏透明覆盖窗口
- [ ] M2 选区交互
- [ ] M3 屏幕捕获
- [ ] M4 QR 识别
- [ ] M5 剪贴板 + 提示
- [ ] M6 全局快捷键 + 托盘
- [ ] M7 多显示器兼容

## 项目结构

```
src/
├── EarthShot.Core/        headless 核心，接口与管道
│   ├── Capturing/         IScreenCapturer, CapturedImage
│   ├── Processing/        IImageProcessor, ProcessingResult
│   ├── Actions/           IResultAction, ActionContext
│   └── Pipeline/          ScreenshotPipeline
│
└── EarthShot.App/         Avalonia UI
    └── Views/             OverlayWindow
```

## 目标框架

- .NET 10
- Avalonia 12
- `IsAotCompatible=true`（Core 库）
- XAML 强制 CompiledBindings
