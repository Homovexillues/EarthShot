using EarthShot.Core.Capturing;
using EarthShot.Core.Processing;

namespace EarthShot.Core.Actions;

public record ActionContext(CapturedImage Source, IReadOnlyList<ProcessingResult> Results);

public interface IResultAction
{
    bool CanHandle(ProcessingResult result);

    Task ExecuteAsync(
        ProcessingResult result,
        ActionContext context,
        CancellationToken cancellationToken
    );
}
