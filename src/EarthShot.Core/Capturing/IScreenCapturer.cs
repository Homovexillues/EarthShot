namespace EarthShot.Core.Capturing;

public interface IScreenCapturer
{
    CapturedImage Capture(PixelRect region);
    PixelRect PrimaryScreenBounds { get; }
}
