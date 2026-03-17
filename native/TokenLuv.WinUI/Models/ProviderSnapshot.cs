using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TokenLuv.WinUI.Models;

public sealed class ProviderSnapshot : INotifyPropertyChanged
{
    private bool _isSelected;

    public required string ProviderId { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required ProviderDataQuality Quality { get; init; }
    public required UsageUnit Unit { get; init; }
    public bool IsConfigured { get; init; } = true;
    public bool HasError { get; init; }
    public double? Used { get; init; }
    public double? Limit { get; init; }
    public string? Model { get; init; }
    public string? CompactLine1Override { get; init; }
    public string? CompactLine2Override { get; init; }
    public string? PrimaryValueOverride { get; init; }
    public string? SecondaryValueOverride { get; init; }
    public double? ProgressPercentOverride { get; init; }
    public string Footnote { get; init; } = string.Empty;
    public DateTimeOffset RefreshedAt { get; init; }
    public IReadOnlyList<ProviderDetailMetric> DetailMetrics { get; init; } = [];
    public string? UsageDashboardUrl { get; init; }
    public string? StatusPageUrl { get; init; }
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TabBackgroundBrush));
            OnPropertyChanged(nameof(TabBorderBrush));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string QualityLabel => Quality switch
    {
        ProviderDataQuality.Real => "Real",
        ProviderDataQuality.Estimated => "Estimated",
        ProviderDataQuality.ValidatedOnly => "Validated only",
        _ => "Unsupported"
    };

    public SolidColorBrush AccentBrush => new(GetProviderColor());

    public SolidColorBrush BorderBrush => new(GetBorderColor());

    public SolidColorBrush TabBackgroundBrush => new(IsSelected
        ? ColorHelper.FromArgb(255, 37, 99, 235)
        : ColorHelper.FromArgb(255, 24, 24, 36));

    public SolidColorBrush TabBorderBrush => new(IsSelected
        ? ColorHelper.FromArgb(255, 59, 130, 246)
        : ColorHelper.FromArgb(255, 42, 42, 58));

    public SolidColorBrush NameBrush => new(Quality == ProviderDataQuality.Unsupported
        ? ColorHelper.FromArgb(255, 71, 85, 105)
        : ColorHelper.FromArgb(255, 203, 213, 225));

    public double RowOpacity => !IsConfigured ? 0.34 : 1;

    public SolidColorBrush PrimaryTextBrush => new(Quality switch
    {
        ProviderDataQuality.Unsupported => ColorHelper.FromArgb(255, 71, 85, 105),
        ProviderDataQuality.ValidatedOnly => ColorHelper.FromArgb(255, 113, 128, 150),
        _ => ColorHelper.FromArgb(255, 148, 163, 184)
    });

    public SolidColorBrush SecondaryTextBrush => new(ColorHelper.FromArgb(255, 51, 65, 85));

    public SolidColorBrush DetailTitleBrush => new(ColorHelper.FromArgb(255, 248, 250, 252));

    public ImageSource LogoSource => ResolveLogoSource();

    public SolidColorBrush TrackBrush => new(ColorHelper.FromArgb(255, 30, 41, 59));

    public SolidColorBrush BarFillBrush => new(GetBarFillColor());

    public string PrimaryValue
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(PrimaryValueOverride))
            {
                return PrimaryValueOverride;
            }

            if (Quality is ProviderDataQuality.ValidatedOnly or ProviderDataQuality.Unsupported)
            {
                return Quality == ProviderDataQuality.ValidatedOnly ? "Key verified" : "No usage API";
            }

            if (Used is null && Limit is null)
            {
                return "No live data";
            }

            return Unit switch
            {
                UsageUnit.Usd => Limit is > 0
                    ? $"{FormatUsd(Math.Max(0, Limit.Value - (Used ?? 0)))} left"
                    : $"{FormatUsd(Used ?? 0)} spent",
                UsageUnit.Tokens => Limit is > 0
                    ? $"{FormatCompact(Math.Max(0, Limit.Value - (Used ?? 0)))} left"
                    : $"{FormatCompact(Used ?? 0)} tokens",
                UsageUnit.Requests => $"{FormatCompact(Used ?? 0)} requests",
                _ => "Unavailable"
            };
        }
    }

    public string SecondaryValue
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SecondaryValueOverride))
            {
                return SecondaryValueOverride;
            }

            if (Quality == ProviderDataQuality.Unsupported)
            {
                return "Use browser automation or provider export as fallback.";
            }

            if (Quality == ProviderDataQuality.ValidatedOnly)
            {
                return Model is { Length: > 0 }
                    ? $"Model detected: {Model}"
                    : "Auth is valid but usage visibility is absent.";
            }

            if (Used is null && Limit is null)
            {
                return "No value returned yet.";
            }

            return Limit is > 0
                ? $"{FormatValue(Used)} / {FormatValue(Limit)}"
                : $"{FormatValue(Used)} with no published limit";
        }
    }

    public double ProgressPercent
    {
        get
        {
            if (ProgressPercentOverride is not null)
            {
                return Math.Clamp(ProgressPercentOverride.Value, 0, 100);
            }

            if (!IsConfigured || HasError)
            {
                return 0;
            }

            if (Quality == ProviderDataQuality.ValidatedOnly)
            {
                return 16;
            }

            if (Quality == ProviderDataQuality.Unsupported)
            {
                return 6;
            }

            if (Used is null)
            {
                return 0;
            }

            if (Limit is > 0)
            {
                return Math.Clamp((Used.Value / Limit.Value) * 100, 0, 100);
            }

            return Quality == ProviderDataQuality.Estimated ? 34 : 24;
        }
    }

    public string RefreshedLabel => $"Refreshed {RefreshedAt.LocalDateTime:t}";
    public string DetailUpdatedLabel => FormatRelativeAge(RefreshedAt);
    public string DetailModeLabel => string.IsNullOrWhiteSpace(Model) ? QualityLabel : Model!;

    public string CompactLine1
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(CompactLine1Override))
            {
                return CompactLine1Override;
            }

            if (Quality == ProviderDataQuality.Real || Quality == ProviderDataQuality.Estimated)
            {
                if (!IsConfigured)
                {
                    return "sin key";
                }

                if (HasError)
                {
                    return "key invalida";
                }

                if (Used is null && Limit is null)
                {
                    return "sin datos aun";
                }

                if (Unit == UsageUnit.Usd)
                {
                    if (Limit is > 0)
                    {
                        return $"{FormatUsd(Math.Max(0, Limit.Value - (Used ?? 0)))} restante";
                    }

                    return $"{FormatUsd(Used ?? 0)} gastado";
                }

                if (Limit is > 0)
                {
                    return $"{FormatCompact(Math.Max(0, Limit.Value - (Used ?? 0)))} restante";
                }

                return $"{FormatCompact(Used ?? 0)} tokens";
            }

            if (!IsConfigured)
            {
                return "sin key";
            }

            if (HasError)
            {
                return "key invalida";
            }

            return Quality == ProviderDataQuality.ValidatedOnly ? "key activa" : "sin API publica";
        }
    }

    public string CompactLine2
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(CompactLine2Override))
            {
                return CompactLine2Override;
            }

            if (Quality == ProviderDataQuality.Real || Quality == ProviderDataQuality.Estimated)
            {
                if (!IsConfigured)
                {
                    return "configuralo en settings";
                }

                if (HasError)
                {
                    return "no pudimos validar la credencial";
                }

                return Limit is > 0
                    ? $"{FormatValue(Used)} / {FormatValue(Limit)}{(Quality == ProviderDataQuality.Estimated ? " · estimated" : string.Empty)}"
                    : Quality == ProviderDataQuality.Estimated ? "estimated · sin limite" : "sin limite";
            }

            if (!IsConfigured)
            {
                return "configuralo en settings";
            }

            if (HasError)
            {
                return "no pudimos validar la credencial";
            }

            return Model is { Length: > 0 }
                ? Model
                : QualityLabel;
        }
    }

    public string AsciiBarText
    {
        get
        {
            const int segments = 18;
            int filled = (int)Math.Round((Math.Clamp(ProgressPercent, 0, 100) / 100d) * segments, MidpointRounding.AwayFromZero);
            filled = Math.Clamp(filled, 0, segments);
            return "[" + new string('=', filled) + new string('.', segments - filled) + "]";
        }
    }

    public double BarFillWidth
    {
        get
        {
            double width = Math.Round(Math.Clamp(ProgressPercent, 0, 100) * 1.28, 1);
            return width < 1 ? 0 : width;
        }
    }

    private Color GetProviderColor() => ProviderId.ToLowerInvariant() switch
    {
        "anthropic" => ColorHelper.FromArgb(255, 232, 112, 64),
        "openai" => ColorHelper.FromArgb(255, 116, 170, 156),
        "openrouter" => ColorHelper.FromArgb(255, 103, 232, 249),
        "antigravity" => ColorHelper.FromArgb(255, 154, 230, 180),
        "xai" => ColorHelper.FromArgb(255, 170, 170, 170),
        "gemini" => ColorHelper.FromArgb(255, 234, 67, 53),
        _ => ColorHelper.FromArgb(255, 139, 92, 246)
    };

    private Color GetBorderColor() => Quality switch
    {
        ProviderDataQuality.Real => ColorHelper.FromArgb(255, 39, 92, 118),
        ProviderDataQuality.Estimated => ColorHelper.FromArgb(255, 110, 93, 38),
        ProviderDataQuality.ValidatedOnly => ColorHelper.FromArgb(255, 62, 70, 88),
        _ => ColorHelper.FromArgb(255, 112, 57, 57)
    };

    private Color GetBarFillColor()
    {
        if (Quality == ProviderDataQuality.Unsupported)
        {
            return ColorHelper.FromArgb(255, 62, 70, 88);
        }

        if (Quality == ProviderDataQuality.ValidatedOnly)
        {
            return ColorHelper.FromArgb(255, 74, 85, 104);
        }

        if (ProgressPercentOverride is > 0)
        {
            double percentage = Math.Clamp(ProgressPercentOverride.Value, 0, 100);
            return ResolveBarColor(percentage);
        }

        if (Limit is > 0 && Used is not null)
        {
            double percentage = Math.Clamp((Used.Value / Limit.Value) * 100, 0, 100);
            return ResolveBarColor(percentage);
        }

        if (Used is > 0)
        {
            return ColorHelper.FromArgb(255, 45, 58, 74);
        }

        return ColorHelper.FromArgb(0, 0, 0, 0);
    }

    private static Color ResolveBarColor(double percentage)
    {
        if (percentage > 80)
        {
            return ColorHelper.FromArgb(255, 239, 68, 68);
        }

        if (percentage > 60)
        {
            return ColorHelper.FromArgb(255, 251, 191, 36);
        }

        return ColorHelper.FromArgb(255, 103, 232, 249);
    }

    private string FormatValue(double? value)
    {
        if (value is null)
        {
            return "n/a";
        }

        return Unit switch
        {
            UsageUnit.Usd => FormatUsd(value.Value),
            UsageUnit.Tokens => FormatCompact(value.Value),
            UsageUnit.Requests => FormatCompact(value.Value),
            _ => value.Value.ToString("N0")
        };
    }

    private static string FormatUsd(double value) => $"${value:N2}";

    private static string FormatCompact(double value)
    {
        if (value >= 1_000_000)
        {
            return $"{value / 1_000_000:0.0}M";
        }

        if (value >= 1_000)
        {
            return $"{value / 1_000:0.#}K";
        }

        return value.ToString("N0");
    }

    private ImageSource ResolveLogoSource()
    {
        string suffix = ProviderId.ToLowerInvariant() switch
        {
            "openai" => "openai.svg",
            "anthropic" => "anthropic.svg",
            "gemini" => "gemini.svg",
            "antigravity" => "antigravity.svg",
            "openrouter" => "openrouter.svg",
            "xai" => "xai.ico",
            _ => "openai.svg"
        };

        string uri = $"ms-appx:///Assets/Providers/{suffix}";
        if (suffix.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return new SvgImageSource(new Uri(uri));
        }

        return new BitmapImage(new Uri(uri));
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
