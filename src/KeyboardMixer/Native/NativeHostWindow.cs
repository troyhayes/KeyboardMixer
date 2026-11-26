using KeyboardMixer.Core;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static KeyboardMixer.Native.Win32;

namespace KeyboardMixer.Native;

/// <summary>
/// A hidden top-level window that receives WM_HOTKEY and tray-icon messages. It's created on the
/// UI thread, so the WinUI message loop dispatches its messages and handlers run on the UI thread.
/// (A message-only window would be enough for hotkeys, but it doesn't receive the
/// "TaskbarCreated" broadcast needed to restore the tray icon after Explorer restarts.)
/// </summary>
internal sealed unsafe class NativeHostWindow : IDisposable
{
    public const uint WM_TRAYICON = WM_APP + 1;
    private const string ClassName = "KeyboardMixer.HostWindow";

    private static NativeHostWindow? s_instance;
    private readonly uint _taskbarCreatedMessage;

    public NativeHostWindow()
    {
        if (s_instance is not null) throw new InvalidOperationException("Only one host window is supported.");
        s_instance = this;

        nint hInstance = GetModuleHandleW(null);
        fixed (char* cls = ClassName)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WndProc,
                hInstance = hInstance,
                lpszClassName = cls,
            };
            RegisterClassExW(&wc); // fails harmlessly if already registered

            Handle = CreateWindowExW((uint)WS_EX_TOOLWINDOW, cls, cls, WS_POPUP, 0, 0, 0, 0, 0, 0, hInstance, null);
        }

        if (Handle == 0) throw new InvalidOperationException($"CreateWindowEx failed ({Marshal.GetLastWin32Error()}).");
        _taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");
    }

    public nint Handle { get; private set; }

    /// <summary>Raised with the hotkey id from RegisterHotKey.</summary>
    public event Action<int>? HotkeyPressed;

    /// <summary>Raised with the raw wParam/lParam of a tray icon callback.</summary>
    public event Action<nint, nint>? TrayMessage;

    /// <summary>Explorer (re)started; tray icons must be added again.</summary>
    public event Action? TaskbarCreated;

    private bool HandleMessage(uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case WM_HOTKEY:
                HotkeyPressed?.Invoke((int)wParam);
                return true;
            case WM_TRAYICON:
                TrayMessage?.Invoke(wParam, lParam);
                return true;
            default:
                if (msg == _taskbarCreatedMessage && _taskbarCreatedMessage != 0)
                {
                    TaskbarCreated?.Invoke();
                    return true;
                }
                return false;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        try
        {
            var instance = s_instance;
            if (instance is not null && instance.Handle == hwnd && instance.HandleMessage(msg, wParam, lParam))
                return 0;
        }
        catch (Exception ex)
        {
            // An exception escaping an UnmanagedCallersOnly method would kill the process.
            Log.Error($"Host window message {msg:X} failed", ex);
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (Handle != 0)
        {
            DestroyWindow(Handle);
            Handle = 0;
        }
        s_instance = null;
    }
}
