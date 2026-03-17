using System.Runtime.InteropServices;
using System.Text;

namespace TokenLuv.WinUI.Services;

public sealed class TrayIconService : IDisposable
{
    private const int GwlWndProc = -4;
    private const uint WmUser = 0x0400;
    private const uint WmTrayIcon = WmUser + 1;
    private const uint WmCommand = 0x0111;
    private const uint WmLButtonUp = 0x0202;
    private const uint WmRButtonUp = 0x0205;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NifShowTip = 0x00000080;
    private const uint NimAdd = 0x00000000;
    private const uint NimModify = 0x00000001;
    private const uint NimDelete = 0x00000002;
    private const uint MfString = 0x00000000;
    private const uint MfSeparator = 0x00000800;
    private const uint TpmLeftAlign = 0x0000;
    private const uint TpmBottomAlign = 0x0020;
    private const uint TpmRightButton = 0x0002;
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x00000010;
    private const uint LrDefaultSize = 0x00000040;

    private readonly IntPtr _windowHandle;
    private readonly Action _showMainWindow;
    private readonly Action _hideMainWindow;
    private readonly Action _openSettingsWindow;
    private readonly Action _refreshPreview;
    private readonly Action _exitApplication;
    private readonly WndProc _newWindowProc;
    private readonly IntPtr _oldWindowProc;
    private readonly IntPtr _iconHandle;
    private bool _disposed;

    private readonly uint _showCommandId = 1001;
    private readonly uint _hideCommandId = 1002;
    private readonly uint _refreshCommandId = 1003;
    private readonly uint _settingsCommandId = 1004;
    private readonly uint _exitCommandId = 1005;

    public readonly record struct TrayAnchorRect(int Left, int Top, int Right, int Bottom);

    public TrayIconService(
        IntPtr windowHandle,
        string iconPath,
        Action showMainWindow,
        Action hideMainWindow,
        Action openSettingsWindow,
        Action refreshPreview,
        Action exitApplication)
    {
        _windowHandle = windowHandle;
        _showMainWindow = showMainWindow;
        _hideMainWindow = hideMainWindow;
        _openSettingsWindow = openSettingsWindow;
        _refreshPreview = refreshPreview;
        _exitApplication = exitApplication;

        _iconHandle = LoadImage(IntPtr.Zero, iconPath, ImageIcon, 0, 0, LrLoadFromFile | LrDefaultSize);
        if (_iconHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Tray icon could not be loaded.");
        }

        _newWindowProc = WindowProcedure;
        _oldWindowProc = SetWindowLongPtr(_windowHandle, GwlWndProc, Marshal.GetFunctionPointerForDelegate(_newWindowProc));

        NotifyIconData data = CreateNotifyIconData("TokenLUV");
        Shell_NotifyIcon(NimAdd, ref data);
    }

    public void UpdateTooltip(string tooltip)
    {
        if (_disposed)
        {
            return;
        }

        string normalized = tooltip.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        if (normalized.Length > 127)
        {
            normalized = normalized[..127];
        }

        NotifyIconData data = CreateNotifyIconData(normalized);
        Shell_NotifyIcon(NimModify, ref data);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        NotifyIconData data = CreateNotifyIconData(string.Empty);
        Shell_NotifyIcon(NimDelete, ref data);
        SetWindowLongPtr(_windowHandle, GwlWndProc, _oldWindowProc);
        DestroyIcon(_iconHandle);
        _disposed = true;
    }

    public TrayAnchorRect? GetAnchorRect()
    {
        NotifyIconIdentifier identifier = new()
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
            hWnd = _windowHandle,
            uID = 1
        };

        int result = Shell_NotifyIconGetRect(ref identifier, out Rect iconRect);
        if (result != 0)
        {
            return null;
        }

        return new TrayAnchorRect(iconRect.Left, iconRect.Top, iconRect.Right, iconRect.Bottom);
    }

    private NotifyIconData CreateNotifyIconData(string tooltip)
    {
        return new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _windowHandle,
            uID = 1,
            uFlags = NifMessage | NifIcon | NifTip | NifShowTip,
            uCallbackMessage = WmTrayIcon,
            hIcon = _iconHandle,
            szTip = tooltip
        };
    }

    private IntPtr WindowProcedure(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WmTrayIcon)
        {
            uint eventId = unchecked((uint)lParam.ToInt64());
            if (eventId == WmLButtonUp)
            {
                _showMainWindow();
                return IntPtr.Zero;
            }

            if (eventId == WmRButtonUp)
            {
                ShowContextMenu();
                return IntPtr.Zero;
            }
        }
        else if (msg == WmCommand)
        {
            uint commandId = LowWord(wParam);
            switch (commandId)
            {
                case 1001:
                    _showMainWindow();
                    return IntPtr.Zero;
                case 1002:
                    _hideMainWindow();
                    return IntPtr.Zero;
                case 1003:
                    _refreshPreview();
                    return IntPtr.Zero;
                case 1004:
                    _openSettingsWindow();
                    return IntPtr.Zero;
                case 1005:
                    _exitApplication();
                    return IntPtr.Zero;
            }
        }

        return CallWindowProc(_oldWindowProc, hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        IntPtr menu = CreatePopupMenu();
        try
        {
            AppendMenu(menu, MfString, _showCommandId, "Show");
            AppendMenu(menu, MfString, _hideCommandId, "Hide");
            AppendMenu(menu, MfString, _refreshCommandId, "Refresh");
            AppendMenu(menu, MfString, _settingsCommandId, "Settings");
            AppendMenu(menu, MfSeparator, 0, string.Empty);
            AppendMenu(menu, MfString, _exitCommandId, "Exit");

            GetCursorPos(out Point point);
            SetForegroundWindow(_windowHandle);
            TrackPopupMenuEx(menu, TpmLeftAlign | TpmBottomAlign | TpmRightButton, point.X, point.Y, _windowHandle, IntPtr.Zero);
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private static uint LowWord(IntPtr value) => unchecked((uint)((ulong)value.ToInt64() & 0xFFFF));

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public Guid guidItem;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData lpData);

    [DllImport("shell32.dll")]
    private static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier identifier, out Rect iconLocation);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
