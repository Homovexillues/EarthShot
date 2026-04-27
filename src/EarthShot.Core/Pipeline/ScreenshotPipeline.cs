using EarthShot.Core.Actions;
using EarthShot.Core.Capturing;
using EarthShot.Core.Processing;

namespace EarthShot.Core.Pipeline;

public sealed class ScreenshotPipeline
{
    private readonly IScreenCapturer _capturer;
    private readonly IReadOnlyList<IImageProcessor> _processors;
    private readonly IReadOnlyList<IResultAction> _actions;

    public ScreenshotPipeline(
        IScreenCapturer capturer,
        IEnumerable<IImageProcessor> processors,
        IEnumerable<IResultAction> actions
    )
    {
        _capturer = capturer;
        _processors = processors.ToArray();
        _actions = actions.ToArray();
    }

    public async Task<IReadOnlyList<ProcessingResult>> RunAsync(
        PixelRect region,
        CancellationToken cancellationToken = default
    )
    {
        if (region.IsEmpty)
            return Array.Empty<ProcessingResult>();

        var image = _capturer.Capture(region);
        if (image.IsEmpty)
            return Array.Empty<ProcessingResult>();

        var results = _processors.SelectMany(p => p.Process(image)).ToArray();

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
