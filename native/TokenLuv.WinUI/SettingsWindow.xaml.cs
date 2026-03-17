using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using TokenLuv.WinUI.Models;
using TokenLuv.WinUI.Services;
using TokenLuv.WinUI.Services.Settings;
using WinRT.Interop;

namespace TokenLuv.WinUI;

public sealed partial class SettingsWindow : Window
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

    private readonly AppSettingsService _settingsService;
    private readonly Func<Task> _onSettingsSavedAsync;
    private AppWindow? _appWindow;
    private string _statusMessage = "PasswordVault + explicit auth only";

    public SettingsWindow(AppSettingsService settingsService, Func<Task> onSettingsSavedAsync)
    {
        _settingsService = settingsService;
        _onSettingsSavedAsync = onSettingsSavedAsync;
        Providers = [];
        RefreshOptions = [1, 5, 15, 30];

        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(HeaderBar);
        HeaderBar.PointerPressed += HeaderBar_PointerPressed;
        ConfigureWindow();
        _ = LoadAsync();
    }

    public ObservableCollection<ProviderSettingsEditorItem> Providers { get; }

    public IReadOnlyList<int> RefreshOptions { get; }

    public int SelectedRefreshInterval { get; set; } = 5;

    public bool SoundAlertsEnabled { get; set; } = true;

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            _statusMessage = value;
            Bindings.Update();
        }
    }

    public void PositionNear(int anchorX, int anchorY, int anchorWidth, int anchorHeight)
    {
        if (_appWindow is null)
        {
            return;
        }

        Rect anchor = new()
        {
            Left = anchorX,
            Top = anchorY,
            Right = anchorX + anchorWidth,
            Bottom = anchorY + anchorHeight
        };

        MoveWithinWorkArea(anchor, alignToWidget: true);
    }

    public void PositionNear(TrayIconService.TrayAnchorRect anchorRect)
    {
        if (_appWindow is null)
        {
            return;
        }

        Rect anchor = new()
        {
            Left = anchorRect.Left,
            Top = anchorRect.Top,
            Right = anchorRect.Right,
            Bottom = anchorRect.Bottom
        };

        MoveWithinWorkArea(anchor, alignToWidget: false);
    }

    private async Task LoadAsync()
    {
        AppSettingsSnapshot settings = await _settingsService.LoadAsync();
        SelectedRefreshInterval = settings.RefreshIntervalMinutes;
        SoundAlertsEnabled = settings.SoundAlertsEnabled;
        Providers.Clear();

        foreach (ProviderDefinition definition in ProviderCatalog.All)
        {
            settings.ProviderCredentials.TryGetValue(definition.ProviderId, out ProviderCredentials? credentials);
            credentials ??= new ProviderCredentials { ProviderId = definition.ProviderId };

            Providers.Add(new ProviderSettingsEditorItem
            {
                ProviderId = definition.ProviderId,
                DisplayName = definition.DisplayName,
                ApiKeyLabel = definition.ApiKeyLabel,
                ConnectedAuthLabel = definition.ConnectedAuthLabel,
                SupportsConnectedAuth = definition.SupportsConnectedAuth,
                ApiKeyPlaceholder = definition.ApiKeyPlaceholder,
                ProvisioningKeyLabel = definition.ProvisioningKeyLabel,
                ProvisioningKeyPlaceholder = definition.ProvisioningKeyPlaceholder,
                AuthButtonLabel = definition.AuthButtonLabel,
                AuthCommand = definition.AuthCommand,
                AuthArguments = definition.AuthArguments,
                OpenButtonLabel = definition.OpenButtonLabel,
                OpenUrl = definition.OpenUrl,
                ApiKey = credentials.ApiKey,
                ProvisioningKey = credentials.ProvisioningKey,
                UseConnectedAuth = credentials.UseConnectedAuth,
                Note = definition.Note ?? string.Empty
            });
        }

        Bindings.Update();
    }

    private void ConfigureWindow()
    {
        IntPtr hWnd = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow.Resize(new Windows.Graphics.SizeInt32(520, 620));
        _appWindow.Title = "TokenLUV Settings";

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

    private void MoveWithinWorkArea(Rect anchor, bool alignToWidget)
    {
        if (_appWindow is null)
        {
            return;
        }

        IntPtr monitorHandle = MonitorFromRect(ref anchor, MonitorDefaultToNearest);
        MonitorInfo monitorInfo = new() { cbSize = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitorHandle, ref monitorInfo))
        {
            return;
        }

        int width = _appWindow.Size.Width;
        int height = _appWindow.Size.Height;

        int targetX = alignToWidget
            ? anchor.Right - width
            : anchor.Right - width + 12;
        int targetY = alignToWidget
            ? anchor.Top + 18
            : anchor.Top - height - 12;

        int minX = monitorInfo.rcWork.Left + 8;
        int maxX = monitorInfo.rcWork.Right - width - 8;
        int minY = monitorInfo.rcWork.Top + 8;
        int maxY = monitorInfo.rcWork.Bottom - height - 8;

        if (targetY < minY)
        {
            targetY = anchor.Bottom + 12;
        }

        targetX = Math.Clamp(targetX, minX, Math.Max(minX, maxX));
        targetY = Math.Clamp(targetY, minY, Math.Max(minY, maxY));

        _appWindow.Move(new Windows.Graphics.PointInt32(targetX, targetY));
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

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        StatusMessage = "saving...";

        Dictionary<string, ProviderCredentials> credentials = new(StringComparer.OrdinalIgnoreCase);
        foreach (ProviderSettingsEditorItem provider in Providers)
        {
            credentials[provider.ProviderId] = new ProviderCredentials
            {
                ProviderId = provider.ProviderId,
                ApiKey = provider.ApiKey,
                ProvisioningKey = provider.ProvisioningKey,
                UseConnectedAuth = provider.UseConnectedAuth
            };
        }

        await _settingsService.SaveAsync(new AppSettingsSnapshot
        {
            RefreshIntervalMinutes = SelectedRefreshInterval,
            SoundAlertsEnabled = SoundAlertsEnabled,
            ProviderCredentials = credentials
        });

        await _onSettingsSavedAsync();
        StatusMessage = "saved";
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ProviderAuthButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ProviderSettingsEditorItem item })
        {
            return;
        }

        try
        {
            item.UseConnectedAuth = true;
            if (!string.IsNullOrWhiteSpace(item.AuthCommand))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = item.AuthCommand,
                    Arguments = item.AuthArguments ?? string.Empty,
                    UseShellExecute = true
                });
                StatusMessage = $"launched {item.DisplayName} auth";
                Bindings.Update();
                return;
            }

            if (!string.IsNullOrWhiteSpace(item.OpenUrl))
            {
                OpenUrl(item.OpenUrl);
                StatusMessage = $"opened {item.DisplayName}";
                Bindings.Update();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"could not launch {item.DisplayName}: {ex.Message}";
        }
    }

    private void ProviderOpenButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ProviderSettingsEditorItem item } || string.IsNullOrWhiteSpace(item.OpenUrl))
        {
            return;
        }

        try
        {
            OpenUrl(item.OpenUrl);
            StatusMessage = $"opened {item.DisplayName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"could not open {item.DisplayName}: {ex.Message}";
        }
    }

    private static void OpenUrl(string url)
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
