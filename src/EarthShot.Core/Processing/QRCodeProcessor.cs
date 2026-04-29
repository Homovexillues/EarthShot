using EarthShot.Core.Capturing;
using ZXing;

namespace EarthShot.Core.Processing;

internal sealed class QRCodeProcessor : IImageProcessor
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
        BarcodeReaderGeneric reader = new()
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = new[] { BarcodeFormat.QR_CODE },
            },
        };
        if (image.IsEmpty)
        {
            yield break;
        }
        // 将CapturedImage转换为ZXing可处理的BinaryBitmap
        var bytes = image.Pixels.ToArray();
        var source = new RGBLuminanceSource(
            bytes,
            image.Width,
            image.Height,
            RGBLuminanceSource.BitmapFormat.BGRA32
        );
        Result? result = null;
        try
        {
            result = reader.Decode(source);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[QR] EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"
            );
        }
        if (result is null)
            yield break;
        // 此时用的范围是假的，因为ZXing没有提供二维码位置的接口，所以暂时只能返回整个图像的范围，等以后找到更好的库或者自己实现二维码解码时再改这里。
        yield return new QrCodeResult(result.Text, 0, 0, image.Width, image.Height);
    }
}
