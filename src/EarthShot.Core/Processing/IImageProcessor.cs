using EarthShot.Core.Capturing;

namespace EarthShot.Core.Processing;

/// <summary>
/// 图像处理器接口，定义了处理图像的方法和返回结果的类型。
/// </summary>
public interface IImageProcessor
{
    string Name { get; }

    /// <summary>
    /// 处理图像并返回处理结果。
    /// </summary>
    /// <param name="image">要处理的图像</param>
    /// <returns>处理结果的集合</returns>
    IEnumerable<ProcessingResult> Process(CapturedImage image);
}
