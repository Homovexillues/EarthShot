using EarthShot.Core.Capturing;

namespace EarthShot.Core.Processing;

public interface IImageProcessor
{
    string Name { get; }

    IEnumerable<ProcessingResult> Process(CapturedImage image);
}
