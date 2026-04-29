namespace EarthShot.Core.Processing;

/// <summary>
/// 处理结果
/// </summary>
public abstract record ProcessingResult;

/// <summary>
/// 二维码结果
/// </summary>
/// <param name="Text">二维码内容</param>
/// <param name="X">二维码左上角 X 坐标</param>
/// <param name="Y">二维码左上角 Y 坐标</param>
/// <param name="Width">二维码宽度</param>
/// <param name="Height">二维码高度</param>
public sealed record QrCodeResult(string Text, int X, int Y, int Width, int Height)
    : ProcessingResult;
