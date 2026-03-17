using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using TokenLuv.WinUI.Services;
using TokenLuv.WinUI.Models;
using TokenLuv.WinUI.ViewModels;
using WinRT.Interop;

namespace TokenLuv.WinUI;

public sealed partial class MainWindow : Window
{
    private const int WmNclButtonDown = 0x00A1;
    private const int HtCaption = 0x0002;
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const nint WsCaption = 0x00C00000;
    private const nint WsThickFrame = 0x00040000;
    private const nint WsMinimizeBox = 0x00020000;
    private const nint WsMaximizeBox = 0x00010000;
    private const nint WsExDlgModalFrame = 0x00000001;
    private const nint WsExClientEdge = 0x00000200;
    private const nint WsExStaticEdge = 0x00020000;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint MonitorDefaultToNearest = 0x00000002;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    private AppWindow? _appWindow;
    private bool _allowClose;

    public MainWindow(DashboardViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Title = "TokenLUV";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(HeaderBar);
        HeaderBar.PointerPressed += HeaderBar_PointerPressed;
        ViewModel.PropertyChanged += (_, _) => TrayTooltipRequested?.Invoke(this, ViewModel.TrayTooltip);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ConfigureWindow();
    }

    public DashboardViewModel ViewModel { get; }

    public bool IsWindowVisible { get; private set; } = true;

    public event EventHandler? SettingsRequested;

    public event EventHandler<string>? TrayTooltipRequested;

    public (int X, int Y, int Width, int Height)? GetPlacement()
    {
        if (_appWindow is null)
        {
            return null;
        }

        return (_appWindow.Position.X, _appWindow.Position.Y, _appWindow.Size.Width, _appWindow.Size.Height);
    }

    public void ShowFromTray(TrayIconService.TrayAnchorRect? anchorRect = null)
    {
        if (_appWindow is null)
        {
            return;
        }

        if (anchorRect is not null)
        {
            MoveAboveTray(anchorRect.Value);
        }

        _appWindow.Show();
        Activate();
        IsWindowVisible = true;
    }

    public void HideToTray()
    {
        _appWindow?.Hide();
        IsWindowVisible = false;
    }

    public void PrepareForExit()
    {
        _allowClose = true;
    }

    private void ConfigureWindow()
    {
        IntPtr hWnd = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.Title = "TokenLUV";
        _appWindow.Resize(new Windows.Graphics.SizeInt32(500, 590));
        _appWindow.Closing += AppWindow_Closing;

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        ApplyWindowChrome(hWnd);
    }

    private static void ApplyWindowChrome(IntPtr hWnd)
    {
        nint style = GetWindowLongPtr(hWnd, GwlStyle);
        style &= ~WsCaption;
        style &= ~WsThickFrame;
        style &= ~WsMinimizeBox;
        style &= ~WsMaximizeBox;
        SetWindowLongPtr(hWnd, GwlStyle, style);

        nint exStyle = GetWindowLongPtr(hWnd, GwlExStyle);
        exStyle &= ~WsExDlgModalFrame;
        exStyle &= ~WsExClientEdge;
        exStyle &= ~WsExStaticEdge;
        SetWindowLongPtr(hWnd, GwlExStyle, exStyle);

        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);

        uint borderColor = 0xFFFFFFFE;
        uint captionColor = 0x00181111;
        uint lightText = 0x00FCFAF8;
        DwmSetWindowAttribute(hWnd, DwmwaBorderColor, ref borderColor, Marshal.SizeOf<uint>());
        DwmSetWindowAttribute(hWnd, DwmwaCaptionColor, ref captionColor, Marshal.SizeOf<uint>());
        DwmSetWindowAttribute(hWnd, DwmwaTextColor, ref lightText, Marshal.SizeOf<uint>());
    }

    private void MoveAboveTray(TrayIconService.TrayAnchorRect anchorRect)
    {
        if (_appWindow is null)
        {
            return;
        }

        Rect monitorRect = new()
        {
            Left = anchorRect.Left,
            Top = anchorRect.Top,
            Right = anchorRect.Right,
            Bottom = anchorRect.Bottom
        };

        IntPtr monitorHandle = MonitorFromRect(ref monitorRect, MonitorDefaultToNearest);
        MonitorInfo monitorInfo = new() { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitorHandle, ref monitorInfo))
        {
            return;
        }

        int width = _appWindow.Size.Width;
        int height = _appWindow.Size.Height;
        int trayCenterX = anchorRect.Left + ((anchorRect.Right - anchorRect.Left) / 2);
        int targetX = trayCenterX - (width / 2);
        int targetY = anchorRect.Top - height - 10;

        int minX = monitorInfo.rcWork.Left + 8;
        int maxX = monitorInfo.rcWork.Right - width - 8;
        int minY = monitorInfo.rcWork.Top + 8;
        int maxY = monitorInfo.rcWork.Bottom - height - 8;

        targetX = Math.Clamp(targetX, minX, Math.Max(minX, maxX));
        targetY = targetY < minY ? anchorRect.Bottom + 10 : targetY;
        targetY = Math.Clamp(targetY, minY, Math.Max(minY, maxY));

        _appWindow.Move(new Windows.Graphics.PointInt32(targetX, targetY));
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        args.Cancel = true;
        HideToTray();
    }

    private void HeaderBar_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(HeaderBar).Properties.IsLeftButtonPressed)
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            ReleaseCapture();
            SendMessage(hWnd, WmNclButtonDown, HtCaption, 0);
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
        Bindings.Update();
    }

    private void ProviderRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string providerId })
        {
            return;
        }

        ViewModel.ToggleProvider(providerId);
        ResizeForSelection();
        Bindings.Update();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        HideToTray();
    }

    private void OpenUsageButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ProviderSnapshot snapshot } || string.IsNullOrWhiteSpace(snapshot.UsageDashboardUrl))
        {
            return;
        }

        OpenExternal(snapshot.UsageDashboardUrl);
    }

    private void OpenStatusButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ProviderSnapshot snapshot } || string.IsNullOrWhiteSpace(snapshot.StatusPageUrl))
        {
            return;
        }

        OpenExternal(snapshot.StatusPageUrl);
    }

    private void ScrollTabsLeft_Click(object sender, RoutedEventArgs e)
    {
        ShiftTabStrip(-180);
    }

    private void ScrollTabsRight_Click(object sender, RoutedEventArgs e)
    {
        ShiftTabStrip(180);
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DashboardViewModel.IsExpanded) or nameof(DashboardViewModel.SelectedProvider))
        {
            ResizeForSelection();
            Bindings.Update();
        }
    }

    private void ResizeForSelection()
    {
        if (_appWindow is null)
        {
            return;
        }

        int targetHeight = 590;
        if (_appWindow.Size.Height != targetHeight)
        {
            _appWindow.Resize(new Windows.Graphics.SizeInt32(500, targetHeight));
        }
    }

    private void ShiftTabStrip(double delta)
    {
        if (ProvidersTabScrollViewer is null)
        {
            return;
        }

        double offset = Math.Max(0, ProvidersTabScrollViewer.HorizontalOffset + delta);
        _ = ProvidersTabScrollViewer.ChangeView(offset, null, null, true);
    }

    private static void OpenExternal(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public int dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref Rect lprc, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint pvAttribute, int cbAttribute);
}
