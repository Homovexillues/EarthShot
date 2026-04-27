namespace EarthShot.Core.Capturing;

public enum PixelFormat
{
    Bgra8888,
    Rgba8888,
}

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
