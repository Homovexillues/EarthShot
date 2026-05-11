using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace EarthShot.App.Services;

public sealed class HotKeyService(Action onHotKeyPressed) : IDisposable
{
    /// <summary>
    /// 热键ID
    /// 这就是个整数标识符，用来区分你注册的不同热键。
    /// 假如你注册了两个热键（Ctrl+S 和 Ctrl+Shift+A），它俩的 ID 不同，消息回调时通过 msg.wParam 拿到 ID，
    /// 你就能知道是哪个热键被按了。选 8637 只是随便挑了个不跟系统保留 ID（0- 大概 5000 以内）冲突的数。
    /// </summary>
    private static readonly UIntPtr HOTKEY_ID = (UIntPtr)8637;

    /// <summary>
    /// 修饰键
    /// 0x0002 和 0x2 值是一样的，写长只是让读者一眼看出这是一个32 位标志位，每一位代表一个开关。
    /// </summary>
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_ALT = 0x0001;

    /// <summary>
    /// 只有真的按下抬起才出发，避免反复触发
    /// </summary>
    private const uint MOD_NOREPEAT = 0x4000;

    /// <summary>
    /// 虚拟键码 - 'X'
    /// Windows 给键盘上每个键分配的虚拟键码。
    /// X 键是第 88 个（十进制），转成十六进制就是 0x58。
    /// 0x 前缀表示后面跟着的是十六进制数（0-9 A-F），即 0x58 = 5×16 + 8 = 88。
    /// </summary>
    private const uint VK_X = 0x58;

    /// <summary>
    /// Windows 消息常量
    /// </summary>
    private const uint WM_HOTKEY = 0x0312;

    /// <summary>
    /// Windows 消息编号 0x0012，含义是"线程该退出了"。
    /// GetMessage 读到 WM_QUIT 时会返回 0，循环就结束了。
    /// </summary>
    private const uint WM_QUIT = 0x0012;

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public UIntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    /// <summary>
    /// 热键监听线程。
    /// 之所以不用Task.Run是因为注册是和线程绑定的，
    /// 如果用了线程池线程，然后又回收线程，热键就失效了。用个专门的线程就不会有这个问题。
    /// </summary>
    private Thread? _listenerThread;
    private uint _listenerThreadId;

    /// <summary>
    /// 热键触发时的回调。这个Action应该由外部传入，通常是调用截图协调器的RequestSession方法。
    /// </summary>
    private readonly Action _onHotKeyPressed =
        onHotKeyPressed ?? throw new ArgumentNullException(nameof(onHotKeyPressed));

    /// <summary>
    /// 是否正在监听全局热键
    /// </summary>
    private volatile bool _running;

    /// <summary>
    /// 启动全局热键监听服务。会在后台线程注册系统热键，并监听其触发事件。
    /// </summary>
    public void Start()
    {
        _running = true;
        _listenerThread = new Thread(ListenerLoop)
        {
            // 设置为后台线程，确保不会阻止应用程序退出
            IsBackground = true,
            Name = "HotKeyListenerThread",
        };
        _listenerThread.Start();
    }

    private void ListenerLoop()
    {
        _listenerThreadId = GetCurrentThreadId();
        // 注册热键失败，可能是权限问题或者热键冲突等
        if (
            // 必须监听线程内部调用
            !RegisterHotKey(IntPtr.Zero, (int)HOTKEY_ID, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_X)
        )
        {
            Console.Error.WriteLine("Failed to register global hotkey.");
            return;
        }
        try
        {
            // 当_running为false且WM_QUIT消息被发送时，GetMessage会返回0，线程就会退出
            while (_running && GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                // 处理消息
                if (msg.message == WM_HOTKEY && msg.wParam == HOTKEY_ID)
                {
                    // 热键被触发，调用回调
                    _onHotKeyPressed();
                    continue;
                }
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        catch (Exception ex)
        {
            // 这里可以记录日志，或者以其他方式处理异常
            Console.Error.WriteLine($"HotKeyService encountered an error: {ex}");
        }
        finally
        {
            // 确保资源清理，例如取消注册热键等
            UnregisterHotKey(IntPtr.Zero, (int)HOTKEY_ID);
        }
    }

    public void Dispose()
    {
        // 这里应该取消注册全局热键，释放资源等
        _running = false;
        // 发送WM_QUIT消息，确保GetMessage退出
        // PostThreadMessage 在线程尚未进入消息循环时是静默失败的（返回 false），不会造成任何危害，所以不需要检查返回值
        PostThreadMessage(_listenerThreadId, WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
        // 等待线程结束，避免资源泄漏
        _listenerThread?.Join(TimeSpan.FromSeconds(1));
    }

    // 下面这些P/Invoke调用的封装等级都应该是private，因为它们是HotKeyService内部实现细节，不需要暴露给外部使用者。

    /// <summary>
    /// 获取当前线程ID。这个ID是一个无符号整数，唯一标识当前线程。
    /// 我们需要这个ID来向线程发送消息（例如WM_QUIT），以便它能正确地响应并退出。
    /// </summary>
    /// <returns></returns>
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    /// <summary>
    /// 注册全局热键。这个方法调用后，系统会监听指定的热键组合（例如Ctrl+Shift+S），当用户按下这个组合时，系统会发送一个WM_HOTKEY消息到应用程序的消息队列中。
    /// </summary>
    /// <param name="hWnd">窗口句柄，如果为IntPtr.Zero，则热键与线程关联</param>
    /// <param name="id">热键ID，用于区分不同的热键</param>
    /// <param name="fsModifiers">修饰键，如MOD_CONTROL、MOD_SHIFT等</param>
    /// <param name="vk">虚拟键码，如VK_S</param>
    /// <returns>如果注册成功，返回true；否则返回false</returns>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(
        uint idThread,
        uint Msg,
        UIntPtr wParam,
        IntPtr lParam
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(
        out MSG lpMsg,
        IntPtr hWnd,
        uint wMsgFilterMin,
        uint wMsgFilterMax
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);
}
