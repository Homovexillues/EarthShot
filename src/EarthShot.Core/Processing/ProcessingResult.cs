namespace EarthShot.Core.Processing;

public abstract record ProcessingResult;

public sealed record QrCodeResult(string Text, int X, int Y, int Width, int Height)
    : ProcessingResult;
