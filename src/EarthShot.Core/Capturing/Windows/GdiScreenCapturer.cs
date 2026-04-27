using System.Runtime.InteropServices;

namespace EarthShot.Core.Capturing.Windows;

public sealed class GdiScreenCapturer : IScreenCapturer
{
    /// <summary>
    /// 返回设备上下文环境的句柄，整个屏幕为设备上下文环境。
    /// hWnd参数为NULL或0时，GetDC函数返回整个屏幕的设备上下文环境的句柄。
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    /// <returns>设备上下文环境的句柄</returns>
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    /// <summary>
    /// 返回位图句柄
    /// </summary>
    /// <param name="hdc">设备上下文环境的句柄</param>
    /// <param name="nWidth">位图的宽度</param>
    /// <param name="nHeight">位图的高度</param>
    /// <returns>位图的句柄</returns>
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    /// <summary>
    /// 将指定的对象选入指定的设备上下文环境中，并返回被选入对象的句柄。
    /// </summary>
    /// <param name="hdc">设备上下文环境的句柄</param>
    /// <param name="hgdiobj">要选入的对象的句柄</param>
    /// <returns>被选入对象的句柄</returns>
    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    /// <summary>
    /// 创建一个与指定设备上下文环境兼容的内存设备上下文环境。
    /// </summary>
    /// <param name="hdc">设备上下文环境的句柄</param>
    /// <returns>兼容的设备上下文环境的句柄</returns>
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    /// <summary>
    /// 释放设备上下文环境。
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    /// <param name="hDC">设备上下文环境的句柄</param>
    /// <returns>如果函数成功，返回值为1；如果函数失败，返回值为0。</returns>
    [DllImport("user32.dll", EntryPoint = "ReleaseDC", SetLastError = true)]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    /// <summary>
    /// 从一个源设备上下文环境中复制一个矩形区域到一个目标设备上下文环境中。
    /// </summary>
    /// <param name="hdcDest">目标设备上下文环境的句柄</param>
    /// <param name="xDest">目标矩形的左上角X坐标</param>
    /// <param name="yDest">目标矩形的左上角Y坐标</param>
    /// <param name="w">矩形的宽度</param>
    /// <param name="h">矩形的高度</param>
    /// <param name="hdcSrc">源设备上下文环境的句柄</param>
    /// <param name="xSrc">源矩形的左上角X坐标</param>
    /// <param name="ySrc">源矩形的左上角Y坐标</param>
    /// <param name="rop">光栅操作代码</param>
    /// <returns>如果函数成功，返回值为true；如果函数失败，返回值为false。</returns>
    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(
        IntPtr hdcDest,
        int xDest,
        int yDest,
        int w,
        int h,
        IntPtr hdcSrc,
        int xSrc,
        int ySrc,
        uint rop
    );

    /// <summary>
    /// 光栅操作代码，SRCCOPY表示直接复制源矩形到目标矩形
    /// </summary>
    private const uint SRCCOPY = 0x00CC0020;

    /// <summary>
    /// 从设备上下文环境中获取位图数据。
    /// </summary>
    /// <param name="hdc">设备上下文环境的句柄</param>
    /// <param name="hbmp">位图的句柄</param>
    /// <param name="uStartScan">起始扫描行</param>
    /// <param name="cScanLines">扫描行数</param>
    /// <param name="lpvBits">位图数据缓冲区</param>
    /// <param name="lpbi">位图信息结构</param>
    /// <param name="uUsage">颜色模式</param>
    /// <returns>如果函数成功，返回值为非零；如果函数失败，返回值为零。</returns>
    [DllImport("gdi32.dll", EntryPoint = "GetDIBits")]
    static extern int GetDIBits(
        [In] IntPtr hdc,
        [In] IntPtr hbmp,
        uint uStartScan,
        uint cScanLines,
        [Out] byte[] lpvBits,
        ref BITMAPINFO lpbi,
        DIB_Color_Mode uUsage
    );

    /// <summary>
    /// BITMAPINFO结构包含一个BITMAPINFOHEADER结构和一个颜色表。
    /// 在C里是一个变长数组，但在C#里无法直接表示，所以我们用一个长度为1的数组占位，
    /// 实际使用时只需要保证这个数组存在即可,对于32位位图，颜色表不使用，但必须存在。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public uint[] bmiColors; // 32-bit 用不到，占位即可
    }

    /// <summary>
    /// BITMAPINFOHEADER结构包含有关位图的尺寸和颜色格式的信息。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    /// <summary>
    /// 删除GDI对象，释放资源。对于位图和内存DC等GDI对象，必须调用DeleteObject或DeleteDC来释放，否则会导致资源泄漏。
    /// </summary>
    /// <param name="hObject">要删除的GDI对象的句柄</param>
    /// <returns>如果函数成功，返回值为true；如果函数失败，返回值为false。</returns>
    [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject([In] IntPtr hObject);

    /// <summary>
    /// 删除设备上下文环境，释放资源。对于位图和内存DC等GDI对象，必须调用DeleteObject或DeleteDC来释放，否则会导致资源泄漏。
    /// </summary>
    /// <param name="hdc">要删除的设备上下文环境的句柄</param>
    /// <returns>如果函数成功，返回值为true；如果函数失败，返回值为false。</returns>
    [DllImport("gdi32.dll", EntryPoint = "DeleteDC")]
    private static extern bool DeleteDC([In] IntPtr hdc);

    /// <summary>
    /// GetSystemMetrics函数检索系统范围内的各种系统指标和配置设置。
    /// 通过传递不同的nIndex参数，可以获取屏幕的宽度、高度、工作区大小等信息，
    /// 这些信息对于屏幕捕获和窗口管理等操作非常有用。
    /// </summary>
    /// <param name="nIndex">要检索的系统指标或配置设置的索引:
    /// 1 = SM_CXSCREEN (屏幕宽度)
    /// 2 = SM_CYSCREEN (屏幕高度)
    /// 4 = SM_CYCAPTION (窗口标题栏的高度)
    /// 5 = SM_CXBORDER (窗口边框的宽度)
    /// 10 = SM_CXFULLSCREEN (全屏模式下的屏幕宽度)
    /// 11 = SM_CYFULLSCREEN (全屏模式下的屏幕高度)
    /// 78 = SM_CXWORKAREA (工作区宽度)
    /// 79 = SM_CYWORKAREA (工作区高度)
    /// </param>
    /// <returns>返回指定系统指标或配置设置的值</returns>
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    /// <summary>
    /// 主屏幕的物理像素 bounds。多显示器场景留待 M7 处理。
    /// </summary>
    public PixelRect PrimaryScreenBounds =>
        new(0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));

    /// <summary>
    /// DIB_Color_Mode枚举定义了GetDIBits函数中颜色模式的参数。
    /// DIB_RGB_COLORS表示使用RGB颜色，DIB_PAL_COLORS表示使用调色板索引颜色。
    /// </summary>
    enum DIB_Color_Mode : uint
    {
        DIB_RGB_COLORS = 0,
        DIB_PAL_COLORS = 1,
    }

    /// <summary>
    /// 捕获屏幕指定区域的图像，并返回一个CapturedImage结构，包含像素数据和图像信息。
    /// </summary>
    /// <param name="region">要捕获的屏幕区域</param>
    /// <returns>返回捕获的图像，如果区域为空则返回空的CapturedImage</returns>
    public CapturedImage Capture(PixelRect region)
    {
        if (region.IsEmpty)
        {
            return new CapturedImage();
        }
        IntPtr screenDC = IntPtr.Zero;
        IntPtr memoryDC = IntPtr.Zero;
        IntPtr bitmap = IntPtr.Zero;
        IntPtr oldBitmap = IntPtr.Zero;
        try
        {
            // 获取屏幕设备上下文环境的句柄,参数0获取整个桌面
            screenDC = GetDC(IntPtr.Zero);
            // 创建一个内存DC，色深和屏幕一致
            memoryDC = CreateCompatibleDC(screenDC);
            // 创建一个与屏幕兼容，且大小与要捕获的区域一致的位图
            bitmap = CreateCompatibleBitmap(screenDC, region.Width, region.Height);
            // 将位图选入内存DC，保存原来的对象句柄
            oldBitmap = SelectObject(memoryDC, bitmap);
            // 从屏幕DC的[x,y,w,h]复制到内存DC的[0,0]
            BitBlt(
                memoryDC,
                0,
                0,
                region.Width,
                region.Height,
                screenDC,
                region.X,
                region.Y,
                SRCCOPY
            );
            // 描述我要怎样的字节布局
            var bitmapInfoHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = region.Width,
                // 负值表示顶向下位图
                biHeight = -region.Height,
                biPlanes = 1,
                biBitCount = 32,
                // BI_RGB
                biCompression = 0,
            };
            // 定义一个BITMAPINFO结构，包含上面的BITMAPINFOHEADER和一个占位的颜色表
            var bitmapInfo = new BITMAPINFO
            {
                bmiHeader = bitmapInfoHeader,
                bmiColors = new uint[1], // 占位
            };
            // 用来存位图数据的缓冲区
            byte[] buffer = new byte[region.Width * region.Height * 4];
            // 把bitmap里的像素按照bitmapInfo描述的格式复制到buffer里，
            // 即将GDI的位图数据转换成我们需要的格式
            if (
                GetDIBits(
                    memoryDC,
                    bitmap,
                    0,
                    (uint)region.Height,
                    buffer,
                    ref bitmapInfo,
                    DIB_Color_Mode.DIB_RGB_COLORS
                ) == 0
            )
            {
                // GetDIBits失败，返回空图
                throw new InvalidOperationException("GetDIBits failed.");
            }
            return new CapturedImage(
                buffer,
                region.Width,
                region.Height,
                // GDI的原生顺序就是BGRA(小端序)，32位天然与4字节对齐，所以可以直接返回，不用padding
                region.Width * 4,
                PixelFormat.Bgra8888
            );
        }
        finally
        {
            // 清理GDI对象，避免资源泄漏
            if (oldBitmap != IntPtr.Zero)
                SelectObject(memoryDC, oldBitmap);
            if (bitmap != IntPtr.Zero)
                DeleteObject(bitmap);
            if (memoryDC != IntPtr.Zero)
                DeleteDC(memoryDC);
            if (screenDC != IntPtr.Zero)
                _ = ReleaseDC(IntPtr.Zero, screenDC);
        }
    }
}
