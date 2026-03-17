namespace TokenLuv.WinUI.Models;

public sealed class AppSettingsSnapshot
{
    public int RefreshIntervalMinutes { get; init; } = 5;
    public bool SoundAlertsEnabled { get; init; } = true;
    public IReadOnlyDictionary<string, ProviderCredentials> ProviderCredentials { get; init; } =
        new Dictionary<string, ProviderCredentials>(StringComparer.OrdinalIgnoreCase);
}
