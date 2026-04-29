using EarthShot.Core.Actions;
using EarthShot.Core.Capturing;
using EarthShot.Core.Processing;

namespace EarthShot.Core.Pipeline;

public sealed class ScreenshotPipeline
{
    private readonly IReadOnlyList<IImageProcessor> _processors;
    private readonly IReadOnlyList<IResultAction> _actions;

    public ScreenshotPipeline(
        IEnumerable<IImageProcessor> processors,
        IEnumerable<IResultAction> actions
    )
    {
        _processors = processors.ToArray();
        _actions = actions.ToArray();
    }

    /// <summary>
    /// 执行截图处理流程：接收图像 -> 处理图像 -> 执行结果动作
    /// </summary>
    /// <param name="image">要处理的图像</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>处理结果的列表</returns>
    public async Task<IReadOnlyList<ProcessingResult>> RunAsync(
        CapturedImage image,
        CancellationToken cancellationToken = default
    )
    {
        if (image.IsEmpty)
            return Array.Empty<ProcessingResult>();

        // 处理器是纯 CPU 工作（ZXing 解码可能 100~500ms），扔到后台线程避免阻塞调用者。
        // await 后默认回到调用者线程（如果是 UI 线程，就回到 UI 线程）——所以下面
        // action 的循环是在 UI 线程上执行的，这样 ClipboardAction 等需要碰 UI/平台
        // 资源的 action 不会跨线程出错。
        var results = await Task.Run(
            () => _processors.SelectMany(p => p.Process(image)).ToArray(),
            cancellationToken
        );

        var context = new ActionContext(image, results);

        foreach (var result in results)
        {
            foreach (var action in _actions)
            {
                if (action.CanHandle(result))
                    await action.ExecuteAsync(result, context, cancellationToken);
            }
        }

        return results;
    }
}
