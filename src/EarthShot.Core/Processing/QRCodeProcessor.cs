using System.Runtime.InteropServices;
using EarthShot.Core.Capturing;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace EarthShot.Core.Processing;

public sealed class QRCodeProcessor : IImageProcessor
{
    /// <summary>
    /// 处理器名称，返回"QRCodeProcessor"，用于标识和选择处理器。
    /// </summary>
    public string Name => nameof(QRCodeProcessor);

    /// <summary>
    /// 处理图像并返回处理结果。它将输入的 CapturedImage 转换为 ZXing 可处理的格式，使用 BarcodeReaderGeneric 解码二维码，
    /// </summary>
    /// <param name="image">要处理的图像</param>
    /// <returns>处理结果的枚举</returns>
    public IEnumerable<ProcessingResult> Process(CapturedImage image)
    {
        if (image.IsEmpty)
        {
            yield break;
        }
        using var skBitmap = CreateSKBitmap(image);
        using var scaledBitmap = ScaleBitmap(skBitmap, 2);
        var source = new RGBLuminanceSource(
            scaledBitmap.Bytes,
            scaledBitmap.Width,
            scaledBitmap.Height,
            RGBLuminanceSource.BitmapFormat.BGRA32
        );
        var binary = new BinaryBitmap(new HybridBinarizer(source));
        var hints = new Dictionary<DecodeHintType, object>
        {
            [DecodeHintType.TRY_HARDER] = true,
            [DecodeHintType.POSSIBLE_FORMATS] = new[] { BarcodeFormat.QR_CODE },
        };
        // 优先用 DecodeMultiple——它的扫描策略比 Decode 更"努力"，能 catch
        // 一些 Decode 错过的边缘情况（同样合法的 QR，Decode 偶尔识别不出）。
        // 单个 QR 的场景下，DecodeMultiple 最坏也就返回长度为 1 的数组。
        Result? result = null;
        try
        {
            QRCodeReader reader = new();
            result = reader.decode(binary, hints);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[QR] EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"
            );
        }
        if (result is null)
            yield break;
        yield return new QrCodeResult(result.Text, 0, 0, image.Width, image.Height);
    }

    /// <summary>
    /// 将 CapturedImage 转换为 SKBitmap，以便 ZXing 处理。CapturedImage 通常包含原始像素数据和图像的宽高信息，而 SKBitmap 是 SkiaSharp 库中的一个类，表示位图图像。
    /// </summary>
    /// <param name="image"></param>
    /// <returns></returns>
    private static SKBitmap CreateSKBitmap(CapturedImage image)
    {
        var skImageInfo = new SKImageInfo(
            image.Width,
            image.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Opaque
        );
        // 将CapturedImage转换为ZXing可处理的BinaryBitmap
        var skBitmap = new SKBitmap(skImageInfo);
        Marshal.Copy(image.Pixels.ToArray(), 0, skBitmap.GetPixels(), image.Pixels.Length);
        return skBitmap;
    }

    /// <summary>
    /// 缩放位图
    /// </summary>
    /// <param name="source"></param>
    /// <param name="scale"></param>
    /// <returns></returns>
    private static SKBitmap ScaleBitmap(SKBitmap source, int scale)
    {
        var info = new SKImageInfo(
            source.Width * scale,
            source.Height * scale,
            SKColorType.Bgra8888,
            SKAlphaType.Opaque
        );
        var dest = new SKBitmap(info);
        var options = new SKSamplingOptions();
        source.ScalePixels(dest, options);
        return dest;
    }
}
