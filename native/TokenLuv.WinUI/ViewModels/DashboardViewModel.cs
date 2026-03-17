using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using TokenLuv.WinUI.Models;
using TokenLuv.WinUI.Services.Providers;

namespace TokenLuv.WinUI.ViewModels;

public sealed class DashboardViewModel : INotifyPropertyChanged
{
    private readonly ProviderMonitorService _monitorService;
    private bool _isRefreshing;
    private DateTimeOffset _lastUpdated = DateTimeOffset.Now;
    private string? _selectedProviderId;
    private AppUpdateInfo? _updateInfo;

    public DashboardViewModel(ProviderMonitorService monitorService)
    {
        _monitorService = monitorService;
        Providers = [];
    }

    public ObservableCollection<ProviderSnapshot> Providers { get; }

    public int RealCount => Providers.Count(provider => provider.IsConfigured && !provider.HasError && provider.Quality == ProviderDataQuality.Real);

    public int EstimatedCount => Providers.Count(provider => provider.IsConfigured && !provider.HasError && provider.Quality == ProviderDataQuality.Estimated);

    public int ValidatedOnlyCount => Providers.Count(provider => provider.IsConfigured && !provider.HasError && provider.Quality == ProviderDataQuality.ValidatedOnly);

    public int UnsupportedCount => Providers.Count(provider => provider.IsConfigured && !provider.HasError && provider.Quality == ProviderDataQuality.Unsupported);

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set
        {
            if (_isRefreshing == value)
            {
                return;
            }

            _isRefreshing = value;
            OnPropertyChanged();
        }
    }

    public string LastUpdatedLabel => IsRefreshing
        ? "actualizando..."
        : $"updated {LastUpdated.LocalDateTime:t}";

    public string LastUpdatedRelativeLabel => IsRefreshing
        ? "updating now"
        : FormatRelativeAge(LastUpdated);

    public DateTimeOffset LastUpdated
    {
        get => _lastUpdated;
        private set
        {
            if (_lastUpdated == value)
            {
                return;
            }

            _lastUpdated = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LastUpdatedLabel));
            OnPropertyChanged(nameof(LastUpdatedRelativeLabel));
            OnPropertyChanged(nameof(TrayTooltip));
        }
    }

    public string TrayTooltip
    {
        get
        {
            List<string> lines = ["TokenLUV"];
            ProviderSnapshot? selected = SelectedProvider;
            if (selected is not null && (selected.IsConfigured || selected.HasError))
            {
                lines.Add(ClampLine($"{selected.DisplayName}: {selected.CompactLine1}", 46));
            }

            IReadOnlyList<ProviderSnapshot> configuredProviders = Providers
                .Where(provider => provider.IsConfigured || provider.HasError)
                .ToList();

            if (configuredProviders.Count == 0)
            {
                lines.Add("Sin keys configuradas");
            }
            else
            {
                IEnumerable<string> remainingNames = configuredProviders
                    .Where(provider => !string.Equals(provider.ProviderId, selected?.ProviderId, StringComparison.OrdinalIgnoreCase))
                    .Select(provider => provider.DisplayName);

                string roster = string.Join(", ", remainingNames);
                if (!string.IsNullOrWhiteSpace(roster))
                {
                    lines.Add(ClampLine($"Providers: {roster}", 58));
                }
            }

            if (HasUpdateAvailable)
            {
                lines.Add(ClampLine($"Update available: v{LatestVersionLabel}", 58));
            }

            lines.Add(LastUpdatedRelativeLabel);
            return ClampTooltip(lines, 127);
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsRefreshing)
        {
            return;
        }

        try
        {
            IsRefreshing = true;
            OnPropertyChanged(nameof(LastUpdatedLabel));
            OnPropertyChanged(nameof(LastUpdatedRelativeLabel));

            (IReadOnlyList<ProviderSnapshot> providers, DateTimeOffset refreshedAt) = await _monitorService.RefreshAsync(cancellationToken);
            ReplaceProviders(providers);
            LastUpdated = refreshedAt;
            NotifyAggregatePropertiesChanged();
        }
        finally
        {
            IsRefreshing = false;
            OnPropertyChanged(nameof(LastUpdatedLabel));
            OnPropertyChanged(nameof(LastUpdatedRelativeLabel));
            OnPropertyChanged(nameof(TrayTooltip));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifyClockTick()
    {
        OnPropertyChanged(nameof(LastUpdatedRelativeLabel));
        OnPropertyChanged(nameof(TrayTooltip));
        OnPropertyChanged(nameof(SelectedProvider));
        OnPropertyChanged(nameof(SelectedProviderUpdatedLabel));
    }

    public ProviderSnapshot? SelectedProvider => Providers.FirstOrDefault(provider => provider.IsSelected);
    public string SelectedProviderName => SelectedProvider?.DisplayName ?? string.Empty;
    public string SelectedProviderUpdatedLabel => SelectedProvider?.DetailUpdatedLabel ?? string.Empty;
    public string SelectedProviderModeLabel => SelectedProvider?.DetailModeLabel ?? string.Empty;
    public string SelectedProviderFootnote => SelectedProvider?.Footnote ?? string.Empty;
    public IReadOnlyList<ProviderDetailMetric> SelectedProviderDetailMetrics => SelectedProvider?.DetailMetrics ?? Array.Empty<ProviderDetailMetric>();
    public bool HasUpdateAvailable => _updateInfo is not null;
    public Visibility UpdateVisibility => HasUpdateAvailable ? Visibility.Visible : Visibility.Collapsed;
    public string LatestVersionLabel => _updateInfo?.LatestVersion ?? string.Empty;
    public string UpdateButtonText => HasUpdateAvailable ? $"update v{LatestVersionLabel}" : string.Empty;
    public string UpdateTooltipText => HasUpdateAvailable ? $"Download TokenLUV v{LatestVersionLabel}" : string.Empty;
    public string? UpdateUrl => _updateInfo?.ReleaseUrl;

    public Visibility DetailVisibility => SelectedProvider is null ? Visibility.Collapsed : Visibility.Visible;

    public bool IsExpanded => SelectedProvider is not null;

    public void ToggleProvider(string providerId)
    {
        _selectedProviderId = providerId;
        ApplySelection();
    }

    public void SetUpdateInfo(AppUpdateInfo? updateInfo)
    {
        _updateInfo = updateInfo;
        OnPropertyChanged(nameof(HasUpdateAvailable));
        OnPropertyChanged(nameof(UpdateVisibility));
        OnPropertyChanged(nameof(LatestVersionLabel));
        OnPropertyChanged(nameof(UpdateButtonText));
        OnPropertyChanged(nameof(UpdateTooltipText));
        OnPropertyChanged(nameof(UpdateUrl));
        OnPropertyChanged(nameof(TrayTooltip));
    }

    private void ReplaceProviders(IEnumerable<ProviderSnapshot> providers)
    {
        string? selectedProviderId = _selectedProviderId;
        Providers.Clear();
        foreach (ProviderSnapshot provider in providers)
        {
            provider.IsSelected = string.Equals(provider.ProviderId, selectedProviderId, StringComparison.OrdinalIgnoreCase);
            Providers.Add(provider);
        }

        if (selectedProviderId is not null && Providers.All(provider => !provider.IsSelected))
        {
            _selectedProviderId = null;
        }

        EnsureSelectedProvider();
    }

    private void NotifyAggregatePropertiesChanged()
    {
        OnPropertyChanged(nameof(RealCount));
        OnPropertyChanged(nameof(EstimatedCount));
        OnPropertyChanged(nameof(ValidatedOnlyCount));
        OnPropertyChanged(nameof(UnsupportedCount));
        OnPropertyChanged(nameof(TrayTooltip));
        OnPropertyChanged(nameof(SelectedProvider));
        OnPropertyChanged(nameof(SelectedProviderName));
        OnPropertyChanged(nameof(SelectedProviderUpdatedLabel));
        OnPropertyChanged(nameof(SelectedProviderModeLabel));
        OnPropertyChanged(nameof(SelectedProviderFootnote));
        OnPropertyChanged(nameof(SelectedProviderDetailMetrics));
        OnPropertyChanged(nameof(DetailVisibility));
        OnPropertyChanged(nameof(IsExpanded));
        OnPropertyChanged(nameof(UpdateVisibility));
    }

    private void ApplySelection()
    {
        foreach (ProviderSnapshot provider in Providers)
        {
            provider.IsSelected = _selectedProviderId is not null
                && string.Equals(provider.ProviderId, _selectedProviderId, StringComparison.OrdinalIgnoreCase);
        }

        OnPropertyChanged(nameof(Providers));
        OnPropertyChanged(nameof(SelectedProvider));
        OnPropertyChanged(nameof(SelectedProviderName));
        OnPropertyChanged(nameof(SelectedProviderUpdatedLabel));
        OnPropertyChanged(nameof(SelectedProviderModeLabel));
        OnPropertyChanged(nameof(SelectedProviderFootnote));
        OnPropertyChanged(nameof(SelectedProviderDetailMetrics));
        OnPropertyChanged(nameof(DetailVisibility));
        OnPropertyChanged(nameof(IsExpanded));
        OnPropertyChanged(nameof(TrayTooltip));
    }

    private void EnsureSelectedProvider()
    {
        if (Providers.Count == 0)
        {
            _selectedProviderId = null;
            ApplySelection();
            return;
        }

        if (_selectedProviderId is null || Providers.All(provider => !string.Equals(provider.ProviderId, _selectedProviderId, StringComparison.OrdinalIgnoreCase)))
        {
            ProviderSnapshot? preferred = Providers.FirstOrDefault(provider => provider.IsConfigured)
                ?? Providers.FirstOrDefault();
            _selectedProviderId = preferred?.ProviderId;
        }

        ApplySelection();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string ClampLine(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 3)] + "...";
    }

    private static string ClampTooltip(IEnumerable<string> lines, int maxLength)
    {
        List<string> output = [];

        foreach (string line in lines)
        {
            string candidate = output.Count == 0
                ? line
                : string.Join(Environment.NewLine, output) + Environment.NewLine + line;

            if (candidate.Length > maxLength)
            {
                break;
            }

            output.Add(line);
        }

        return output.Count == 0 ? "TokenLUV" : string.Join(Environment.NewLine, output);
    }

    private static string FormatRelativeAge(DateTimeOffset timestamp)
    {
        TimeSpan age = DateTimeOffset.Now - timestamp;
        if (age.TotalSeconds < 45)
        {
            return "updated just now";
        }

        if (age.TotalMinutes < 60)
        {
            return $"updated {(int)Math.Max(1, age.TotalMinutes)}m ago";
        }

        if (age.TotalHours < 24)
        {
            return $"updated {(int)Math.Max(1, age.TotalHours)}h ago";
        }

        return $"updated {(int)Math.Max(1, age.TotalDays)}d ago";
    }
}
