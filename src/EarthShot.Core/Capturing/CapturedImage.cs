namespace EarthShot.Core.Capturing;

/// <summary>
/// PixelFormat枚举定义了图像的像素格式，目前支持Bgra8888和Rgba8888两种格式。
/// Bgra8888表示每个像素由4个字节组成，分别是蓝色B、绿色G、红色R和Alpha通道A，按小端序存储。
/// Rgba8888表示每个像素由4个字节组成，分别是红色R、绿色G、蓝色B和Alpha通道A，按小端序存储。
/// </summary>
public enum PixelFormat
{
    Bgra8888,
    Rgba8888,
}

/// <summary>
/// CapturedImage结构表示捕获的图像数据，包括像素数据、图像宽度、高度、行距和像素格式等信息。
/// </summary>
/// <param name="Pixels">一个只读内存块，包含图像的像素数据</param>
/// <param name="Width">图像的宽度</param>
/// <param name="Height">图像的高度</param>
/// <param name="Stride">每行像素的字节数</param>
/// <param name="Format">像素格式</param>
public readonly record struct CapturedImage(
    ReadOnlyMemory<byte> Pixels,
    int Width,
    int Height,
    int Stride,
    PixelFormat Format
)
{
    public bool IsEmpty => Pixels.IsEmpty || Width == 0 || Height == 0;
}
