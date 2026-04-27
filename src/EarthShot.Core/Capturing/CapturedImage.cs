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
    /// <summary>
    /// 从当前图像中裁剪出一个指定区域的子图像，并返回一个新的CapturedImage结构。
    /// </summary>
    /// <param name="rect">要裁剪的区域</param>
    /// <returns>裁剪后的子图像</returns>
    public CapturedImage Crop(PixelRect rect)
    {
        if (rect.IsEmpty)
        {
            return default;
        }
        // 范围限制
        rect = new PixelRect(
            Math.Clamp(rect.X, 0, Width - 1),
            Math.Clamp(rect.Y, 0, Height - 1),
            Math.Clamp(rect.Width, 0, Width - rect.X),
            Math.Clamp(rect.Height, 0, Height - rect.Y)
        );
        int bytesPerPixel = 4;
        // 裁剪后的目标图像的像素数据缓冲区
        var destBytes = new byte[rect.Width * rect.Height * bytesPerPixel];
        // 原图像的像素数据缓冲区
        var sourceBytes = Pixels.Span;
        for (int y = 0; y < rect.Height; y++)
        {
            var sourceOffset = (rect.Y + y) * Stride + rect.X * bytesPerPixel;
            var destOffset = y * rect.Width * bytesPerPixel;
            sourceBytes
                .Slice(sourceOffset, rect.Width * bytesPerPixel)
                .CopyTo(destBytes.AsSpan(destOffset));
        }
        return new CapturedImage(
            destBytes,
            rect.Width,
            rect.Height,
            rect.Width * bytesPerPixel,
            Format
        );
    }

    public bool IsEmpty => Pixels.IsEmpty || Width == 0 || Height == 0;
}
