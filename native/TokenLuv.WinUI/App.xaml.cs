using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using TokenLuv.WinUI.Services;
using TokenLuv.WinUI.Services.Providers;
using TokenLuv.WinUI.Services.Security;
using TokenLuv.WinUI.Services.Settings;
using TokenLuv.WinUI.Services.Updates;
using TokenLuv.WinUI.ViewModels;
using WinRT.Interop;

namespace TokenLuv.WinUI;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;
    private TrayIconService? _trayIconService;
    private DashboardViewModel? _dashboardViewModel;
    private AppSettingsService? _settingsService;
    private ProviderMonitorService? _monitorService;
    private AppUpdateService? _updateService;
    private DispatcherQueueTimer? _refreshTimer;
    private DispatcherQueueTimer? _tooltipTimer;
    private readonly Dictionary<string, int> _alertLevels = new(StringComparer.OrdinalIgnoreCase);
    private bool _soundAlertsEnabled = true;
    private bool _alertsPrimed;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _settingsService = new AppSettingsService(new JsonSettingsStore(), new PasswordVaultSecretStore());
        _monitorService = new ProviderMonitorService(_settingsService);
        _updateService = new AppUpdateService();
        _dashboardViewModel = new DashboardViewModel(_monitorService);

        _mainWindow = new MainWindow(_dashboardViewModel);
        _mainWindow.SettingsRequested += (_, _) => OpenSettingsWindow();
        _mainWindow.TrayTooltipRequested += (_, tooltip) => _trayIconService?.UpdateTooltip(tooltip);
        _mainWindow.Activate();

        string iconPath = ResolveIconPath();
        _trayIconService = new TrayIconService(
            WindowNative.GetWindowHandle(_mainWindow),
            iconPath,
            ShowMainWindow,
            HideMainWindow,
            OpenSettingsWindow,
            () => _ = RefreshMainWindowAsync(),
            ExitApplication);

        ConfigureTooltipTimer();
        _ = ReloadSettingsAndRefreshAsync();
        _ = CheckForUpdatesAsync();
    }

    private void ShowMainWindow()
    {
        _mainWindow?.ShowFromTray(_trayIconService?.GetAnchorRect());
    }

    private void HideMainWindow()
    {
        _mainWindow?.HideToTray();
    }

    private async Task RefreshMainWindowAsync()
    {
        if (_dashboardViewModel is null)
        {
            return;
        }

        await _dashboardViewModel.RefreshAsync();
        EvaluateUsageAlerts();
        _trayIconService?.UpdateTooltip(_dashboardViewModel.TrayTooltip);
    }

    private void OpenSettingsWindow()
    {
        if (_settingsWindow is null)
        {
            if (_settingsService is null)
            {
                return;
            }

            _settingsWindow = new SettingsWindow(_settingsService, OnSettingsSavedAsync);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        if (_mainWindow?.IsWindowVisible == true && _mainWindow.GetPlacement() is { } placement)
        {
            _settingsWindow.PositionNear(placement.X, placement.Y, placement.Width, placement.Height);
        }
        else if (_trayIconService?.GetAnchorRect() is { } trayAnchor)
        {
            _settingsWindow.PositionNear(trayAnchor);
        }

        _settingsWindow.Activate();
    }

    private async Task OnSettingsSavedAsync()
    {
        await ReloadSettingsAndRefreshAsync();
        _trayIconService?.UpdateTooltip(_dashboardViewModel?.TrayTooltip ?? "TokenLUV");
    }

    private async Task ReloadSettingsAndRefreshAsync()
    {
        if (_monitorService is null || _dashboardViewModel is null || _mainWindow is null)
        {
            return;
        }

        Models.AppSettingsSnapshot settings = await _monitorService.LoadSettingsAsync();
        _soundAlertsEnabled = settings.SoundAlertsEnabled;
        ConfigureRefreshTimer(settings.RefreshIntervalMinutes);
        await _dashboardViewModel.RefreshAsync();
        EvaluateUsageAlerts();
        _trayIconService?.UpdateTooltip(_dashboardViewModel.TrayTooltip);
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_updateService is null || _dashboardViewModel is null)
        {
            return;
        }

        try
        {
            Models.AppUpdateInfo? updateInfo = await _updateService.CheckForUpdateAsync();
            _dashboardViewModel.SetUpdateInfo(updateInfo);
            _trayIconService?.UpdateTooltip(_dashboardViewModel.TrayTooltip);
        }
        catch
        {
            _dashboardViewModel.SetUpdateInfo(null);
        }
    }

    private void ConfigureRefreshTimer(int refreshIntervalMinutes)
    {
        if (_mainWindow is null)
        {
            return;
        }

        _refreshTimer ??= _mainWindow.DispatcherQueue.CreateTimer();
        _refreshTimer.Stop();
        _refreshTimer.IsRepeating = true;
        _refreshTimer.Interval = TimeSpan.FromMinutes(Math.Clamp(refreshIntervalMinutes, 1, 60));
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();
    }

    private void ConfigureTooltipTimer()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _tooltipTimer ??= _mainWindow.DispatcherQueue.CreateTimer();
        _tooltipTimer.Stop();
        _tooltipTimer.IsRepeating = true;
        _tooltipTimer.Interval = TimeSpan.FromSeconds(15);
        _tooltipTimer.Tick -= TooltipTimer_Tick;
        _tooltipTimer.Tick += TooltipTimer_Tick;
        _tooltipTimer.Start();
    }

    private void RefreshTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        _ = RefreshMainWindowAsync();
    }

    private void TooltipTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        _dashboardViewModel?.NotifyClockTick();
        _trayIconService?.UpdateTooltip(_dashboardViewModel?.TrayTooltip ?? "TokenLUV");
    }

    private void EvaluateUsageAlerts()
    {
        if (_dashboardViewModel is null)
        {
            return;
        }

        if (!_soundAlertsEnabled)
        {
            foreach (Models.ProviderSnapshot provider in _dashboardViewModel.Providers)
            {
                _alertLevels[provider.ProviderId] = ResolveAlertLevel(provider);
            }

            _alertsPrimed = true;
            return;
        }

        foreach (Models.ProviderSnapshot provider in _dashboardViewModel.Providers)
        {
            int currentLevel = ResolveAlertLevel(provider);

            _alertLevels.TryGetValue(provider.ProviderId, out int previousLevel);
            if (_alertsPrimed && currentLevel > previousLevel)
            {
                PlayAlert(currentLevel);
            }

            _alertLevels[provider.ProviderId] = currentLevel;
        }

        _alertsPrimed = true;
    }

    private static int ResolveAlertLevel(Models.ProviderSnapshot provider)
    {
        if (!provider.IsConfigured || provider.HasError || provider.Quality is Models.ProviderDataQuality.ValidatedOnly or Models.ProviderDataQuality.Unsupported)
        {
            return 0;
        }

        return provider.ProgressPercent switch
        {
            >= 95 => 2,
            >= 80 => 1,
            _ => 0
        };
    }

    private static void PlayAlert(int level)
    {
        switch (level)
        {
            case 2:
                _ = Task.Run(() =>
                {
                    try
                    {
                        Console.Beep(1260, 180);
                        Console.Beep(1260, 180);
                    }
                    catch
                    {
                    }
                });
                break;
            case 1:
                _ = Task.Run(() =>
                {
                    try
                    {
                        Console.Beep(960, 180);
                    }
                    catch
                    {
                    }
                });
                break;
        }
    }

    private void ExitApplication()
    {
        _refreshTimer?.Stop();
        _tooltipTimer?.Stop();
        _trayIconService?.Dispose();
        _trayIconService = null;
        _settingsWindow?.Close();
        if (_mainWindow is not null)
        {
            _mainWindow.PrepareForExit();
            _mainWindow.Close();
        }
    }

    private static string ResolveIconPath()
    {
        string assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico");
        if (File.Exists(assetsPath))
        {
            return assetsPath;
        }

        string rootPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        if (File.Exists(rootPath))
        {
            return rootPath;
        }

        throw new FileNotFoundException("Tray icon not found in output.", assetsPath);
    }
}
