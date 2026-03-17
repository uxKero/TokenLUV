using System.Diagnostics;
using TokenLuv.WinUI.Models;

namespace TokenLuv.WinUI.Services.Providers;

public sealed class AntigravityProviderClient : ProviderClientBase
{
    public override string ProviderId => "antigravity";
    public override string DisplayName => "Antigravity";
    public override string Description => "Local-only provider. Windows probe support is still experimental.";
    public override ProviderDataQuality DefaultQuality => ProviderDataQuality.Unsupported;

    public override Task<ProviderSnapshot> FetchAsync(ProviderCredentials credentials, CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        if (!credentials.UseConnectedAuth)
        {
            return Task.FromResult(CreateSnapshot(
                ProviderDataQuality.Unsupported,
                UsageUnit.Unknown,
                now,
                isConfigured: false,
                footnote: "Enable the local probe from settings before TokenLUV inspects the Antigravity runtime.",
                compactLine1Override: "probe disabled",
                compactLine2Override: "enable in settings",
                primaryValueOverride: "probe disabled",
                secondaryValueOverride: "enable in settings",
                detailMetrics:
                [
                    new ProviderDetailMetric
                    {
                        Title = "Local probe",
                        Summary = "disabled",
                        RightLabel = "manual opt-in",
                        Footer = "Antigravity has no public quota API here yet, so TokenLUV only supports an explicit local probe."
                    }
                ],
                usageDashboardUrl: "https://antigravity.ai/",
                statusPageUrl: "https://antigravity.ai/"));
        }

        bool isRunning = Process.GetProcesses().Any(process =>
        {
            try
            {
                string name = process.ProcessName;
                return name.Contains("antigravity", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("language_server", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        });

        ProviderSnapshot snapshot = CreateSnapshot(
            ProviderDataQuality.Unsupported,
            UsageUnit.Unknown,
            now,
            isConfigured: isRunning,
            footnote: "Local probe path reserved for Antigravity desktop runtime.",
            compactLine1Override: isRunning ? "local app ready" : "app not running",
            compactLine2Override: isRunning ? "windows probe experimental" : "launch antigravity first",
            primaryValueOverride: isRunning ? "local app ready" : "app not running",
            secondaryValueOverride: isRunning ? "windows probe experimental" : "launch antigravity first",
            progressPercentOverride: isRunning ? 8 : 0,
            detailMetrics:
            [
                new ProviderDetailMetric
                {
                    Title = "Local probe",
                    Summary = isRunning ? "runtime detected" : "runtime not detected",
                    RightLabel = isRunning ? "experimental" : "launch app",
                    Footer = isRunning
                        ? "Antigravity desktop appears to be running. Usage parsing for Windows is still experimental."
                        : "Launch Antigravity from settings, then refresh TokenLUV."
                }
            ],
            usageDashboardUrl: "https://antigravity.ai/",
            statusPageUrl: "https://antigravity.ai/");

        return Task.FromResult(snapshot);
    }
}
