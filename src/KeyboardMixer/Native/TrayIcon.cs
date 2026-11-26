using static KeyboardMixer.Native.Win32;

namespace KeyboardMixer.Native;

internal readonly record struct TrayMenuItem(int Id, string? Text, bool IsChecked = false)
{
    public static TrayMenuItem Separator => new(0, null);
}

/// <summary>Notification-area icon built directly on Shell_NotifyIcon, like the PowerToys runner.</summary>
internal sealed unsafe class TrayIcon : IDisposable
{
    private const uint IconId = 1;

    private readonly NativeHostWindow _host;
    private readonly string _tooltip;
    private nint _icon;
    private bool _added;

    public TrayIcon(NativeHostWindow host, string iconPath, string tooltip)
    {
        _host = host;
        _tooltip = tooltip;
        _icon = LoadImageW(0, iconPath, IMAGE_ICON, GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON), LR_LOADFROMFILE);

        _host.TrayMessage += OnTrayMessage;
        _host.TaskbarCreated += Add;
        Add();
    }

    public event Action? LeftClick;

    /// <summary>Right-click / context-menu key. Arguments are the screen coordinates to show a menu at.</summary>
    public event Action<int, int>? ContextMenuRequested;

    /// <summary>Shows a native popup menu and returns the chosen item id, or 0 if dismissed.</summary>
    public int ShowMenu(int x, int y, IEnumerable<TrayMenuItem> items)
    {
        nint menu = CreatePopupMenu();
        try
        {
            foreach (var item in items)
            {
                if (item.Text is null)
                    AppendMenuW(menu, MF_SEPARATOR, 0, null);
                else
                    AppendMenuW(menu, MF_STRING | (item.IsChecked ? MF_CHECKED : 0), (nuint)item.Id, item.Text);
            }

            // Required so the menu closes when the user clicks elsewhere (KB135788).
            SetForegroundWindow(_host.Handle);
            int command = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_NONOTIFY | TPM_BOTTOMALIGN, x, y, _host.Handle, null);
            PostMessageW(_host.Handle, WM_NULL, 0, 0);
            return command;
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private void Add()
    {
        var data = CreateData();
        data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP;
        _added = Shell_NotifyIconW(NIM_ADD, &data);
        if (!_added) KeyboardMixer.Core.Log.Info("Tray icon could not be added (Explorer not ready?)");

        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIconW(NIM_SETVERSION, &data);
    }

    private NOTIFYICONDATAW CreateData()
    {
        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = _host.Handle,
            uID = IconId,
            uCallbackMessage = NativeHostWindow.WM_TRAYICON,
            hIcon = _icon,
        };
        var tip = _tooltip.AsSpan(0, Math.Min(_tooltip.Length, 127));
        tip.CopyTo(new Span<char>(data.szTip, 128));
        return data;
    }

    private void OnTrayMessage(nint wParam, nint lParam)
    {
        // With NOTIFYICON_VERSION_4: LOWORD(lParam) = event, wParam = anchor x/y.
        uint evt = (uint)(lParam & 0xFFFF);
        switch (evt)
        {
            case NIN_SELECT:
            case NIN_KEYSELECT:
                LeftClick?.Invoke();
                break;
            case WM_CONTEXTMENU:
                int x = (short)(wParam & 0xFFFF);
                int y = (short)((wParam >> 16) & 0xFFFF);
                ContextMenuRequested?.Invoke(x, y);
                break;
        }
    }

    public void Dispose()
    {
        _host.TrayMessage -= OnTrayMessage;
        _host.TaskbarCreated -= Add;
        if (_added)
        {
            var data = CreateData();
            Shell_NotifyIconW(NIM_DELETE, &data);
            _added = false;
        }
        if (_icon != 0)
        {
            DestroyIcon(_icon);
            _icon = 0;
        }
    }
}
