using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

if (args.Length == 0)
{
    Console.WriteLine("用法: dotnet run --project tools/ZXingProbe -- <PNG 路径>");
    return 1;
}

var path = args[0];
if (!File.Exists(path))
{
    Console.WriteLine($"文件不存在: {path}");
    return 1;
}

using var loaded = SKBitmap.Decode(path);
if (loaded is null)
{
    Console.WriteLine($"无法解码: {path}");
    return 1;
}
Console.WriteLine($"加载: {loaded.Width}x{loaded.Height}, ColorType={loaded.ColorType}");

var hints = new Dictionary<DecodeHintType, object>
{
    [DecodeHintType.TRY_HARDER] = true,
    [DecodeHintType.POSSIBLE_FORMATS] = new[] { BarcodeFormat.QR_CODE },
};

Try("[1] BarcodeReaderGeneric.Decode (Hybrid)", () => DecodeViaReader(loaded, 1, multi: false));
Try(
    "[2] BarcodeReaderGeneric.DecodeMultiple (Hybrid)",
    () => DecodeViaReader(loaded, 1, multi: true)
);
Try(
    "[3] MultiFormatReader + GlobalHistogramBinarizer",
    () => DecodeViaBinarizer(loaded, 1, useGlobal: true)
);
Try("[4] BarcodeReaderGeneric.Decode + 2x 升采样", () => DecodeViaReader(loaded, 2, multi: false));
Try("[5] BarcodeReaderGeneric.Decode + 3x 升采样", () => DecodeViaReader(loaded, 3, multi: false));
Try("[6] QRCodeReader 直接调用 + 2x 升采样", () => DecodeViaQrReader(loaded, 2));

return 0;

void Try(string label, Func<string> action)
{
    Console.WriteLine();
    Console.WriteLine($"--- {label} ---");
    try
    {
        Console.WriteLine($"→ {action()}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"→ EXCEPTION: {ex.GetType().Name}: {ex.Message}");
    }
}

string DecodeViaReader(SKBitmap source, int scale, bool multi)
{
    using var working = ToBgra(source, scale);
    var luminance = new RGBLuminanceSource(
        working.Bytes,
        working.Width,
        working.Height,
        RGBLuminanceSource.BitmapFormat.BGRA32
    );
    var reader = MakeReader();
    if (multi)
    {
        var results = reader.DecodeMultiple(luminance);
        return results is null || results.Length == 0
            ? "EMPTY"
            : string.Join(" | ", results.Select(r => Truncate(r.Text)));
    }
    var result = reader.Decode(luminance);
    return result is null ? "NULL" : Truncate(result.Text);
}

string DecodeViaBinarizer(SKBitmap source, int scale, bool useGlobal)
{
    using var working = ToBgra(source, scale);
    var luminance = new RGBLuminanceSource(
        working.Bytes,
        working.Width,
        working.Height,
        RGBLuminanceSource.BitmapFormat.BGRA32
    );
    Binarizer binarizer = useGlobal
        ? new GlobalHistogramBinarizer(luminance)
        : new HybridBinarizer(luminance);
    var binary = new BinaryBitmap(binarizer);
    var multiReader = new MultiFormatReader();
    var result = multiReader.decode(binary, hints);
    return result is null ? "NULL" : Truncate(result.Text);
}

string DecodeViaQrReader(SKBitmap source, int scale)
{
    using var working = ToBgra(source, scale);
    var luminance = new RGBLuminanceSource(
        working.Bytes,
        working.Width,
        working.Height,
        RGBLuminanceSource.BitmapFormat.BGRA32
    );
    var binary = new BinaryBitmap(new HybridBinarizer(luminance));
    var qrReader = new QRCodeReader();
    var result = qrReader.decode(binary, hints);
    return result is null ? "NULL" : Truncate(result.Text);
}

static BarcodeReaderGeneric MakeReader() =>
    new()
    {
        AutoRotate = true,
        Options = new DecodingOptions
        {
            TryHarder = true,
            TryInverted = true,
            PureBarcode = false,
            PossibleFormats = new[] { BarcodeFormat.QR_CODE },
        },
    };

static SKBitmap ToBgra(SKBitmap source, int scale)
{
    if (scale == 1 && source.ColorType == SKColorType.Bgra8888)
    {
        // 不能 dispose 入参——返回 clone
        var clone = new SKBitmap(
            new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888)
        );
        source.CopyTo(clone, SKColorType.Bgra8888);
        return clone;
    }
    var w = source.Width * scale;
    var h = source.Height * scale;
    var resized = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888));
    if (!source.ScalePixels(resized, SKFilterQuality.High))
    {
        resized.Dispose();
        throw new InvalidOperationException($"ScalePixels failed (scale={scale})");
    }
    return resized;
}

static string Truncate(string s) => s.Length <= 80 ? s : s[..80] + "...";
