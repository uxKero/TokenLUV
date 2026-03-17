using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace TokenLuv.WinUI.Models;

public sealed class ProviderDetailMetric
{
    public required string Title { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string RightLabel { get; init; } = string.Empty;
    public string Footer { get; init; } = string.Empty;
    public double? Percent { get; init; }

    public string PercentLabel => Percent is null ? Summary : $"{Math.Clamp(Percent.Value, 0, 100):0}% used";

    public string AsciiBarText
    {
        get
        {
            const int segments = 28;
            if (Percent is null)
            {
                return "[" + new string('.', segments) + "]";
            }

            int filled = (int)Math.Round((Math.Clamp(Percent.Value, 0, 100) / 100d) * segments, MidpointRounding.AwayFromZero);
            filled = Math.Clamp(filled, 0, segments);
            return "[" + new string('=', filled) + new string('.', segments - filled) + "]";
        }
    }

    public SolidColorBrush MeterBrush => new(ResolveColor());

    private Color ResolveColor()
    {
        if (Percent is null)
        {
            return ColorHelper.FromArgb(255, 74, 85, 104);
        }

        double value = Math.Clamp(Percent.Value, 0, 100);
        if (value >= 95)
        {
            return ColorHelper.FromArgb(255, 239, 68, 68);
        }

        if (value >= 80)
        {
            return ColorHelper.FromArgb(255, 245, 158, 11);
        }

        return ColorHelper.FromArgb(255, 103, 232, 249);
    }
}
