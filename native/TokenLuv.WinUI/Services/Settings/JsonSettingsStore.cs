using System.Text.Json;

namespace TokenLuv.WinUI.Services.Settings;

public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public JsonSettingsStore()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appFolder = Path.Combine(appData, "TokenLuv");
        Directory.CreateDirectory(appFolder);
        _settingsPath = Path.Combine(appFolder, "settings.json");
    }

    public async Task<StoredSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return new StoredSettings();
        }

        await using FileStream stream = File.OpenRead(_settingsPath);
        StoredSettings? stored = await JsonSerializer.DeserializeAsync<StoredSettings>(stream, SerializerOptions, cancellationToken);
        return stored ?? new StoredSettings();
    }

    public async Task SaveAsync(StoredSettings settings, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
    }
}
