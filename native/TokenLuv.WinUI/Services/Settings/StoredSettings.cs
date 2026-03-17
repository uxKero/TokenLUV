namespace TokenLuv.WinUI.Services.Settings;

public sealed class StoredSettings
{
    public int RefreshIntervalMinutes { get; set; } = 5;
    public bool SoundAlertsEnabled { get; set; } = true;
    public List<string> ConnectedAuthProviders { get; set; } = [];
}
