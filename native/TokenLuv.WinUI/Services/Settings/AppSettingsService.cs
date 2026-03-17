using TokenLuv.WinUI.Models;
using TokenLuv.WinUI.Services.Security;

namespace TokenLuv.WinUI.Services.Settings;

public sealed class AppSettingsService
{
    private readonly JsonSettingsStore _settingsStore;
    private readonly ISecretStore _secretStore;

    public AppSettingsService(JsonSettingsStore settingsStore, ISecretStore secretStore)
    {
        _settingsStore = settingsStore;
        _secretStore = secretStore;
    }

    public async Task<AppSettingsSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        StoredSettings stored = await _settingsStore.LoadAsync(cancellationToken);
        Dictionary<string, ProviderCredentials> credentials = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> connectedProviders = stored.ConnectedAuthProviders
            .Where(providerId => !string.IsNullOrWhiteSpace(providerId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (ProviderDefinition definition in ProviderCatalog.All)
        {
            string apiKey = await _secretStore.GetAsync(GetApiKeySecretName(definition.ProviderId), cancellationToken) ?? string.Empty;
            string provisioningKey = await _secretStore.GetAsync(GetProvisioningSecretName(definition.ProviderId), cancellationToken) ?? string.Empty;

            credentials[definition.ProviderId] = new ProviderCredentials
            {
                ProviderId = definition.ProviderId,
                ApiKey = apiKey,
                ProvisioningKey = provisioningKey,
                UseConnectedAuth = connectedProviders.Contains(definition.ProviderId)
            };
        }

        return new AppSettingsSnapshot
        {
            RefreshIntervalMinutes = Math.Clamp(stored.RefreshIntervalMinutes, 1, 60),
            SoundAlertsEnabled = stored.SoundAlertsEnabled,
            ProviderCredentials = credentials
        };
    }

    public async Task SaveAsync(AppSettingsSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _settingsStore.SaveAsync(new StoredSettings
        {
            RefreshIntervalMinutes = Math.Clamp(snapshot.RefreshIntervalMinutes, 1, 60),
            SoundAlertsEnabled = snapshot.SoundAlertsEnabled,
            ConnectedAuthProviders = snapshot.ProviderCredentials.Values
                .Where(credentials => credentials.UseConnectedAuth)
                .Select(credentials => credentials.ProviderId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(providerId => providerId, StringComparer.OrdinalIgnoreCase)
                .ToList()
        }, cancellationToken);

        foreach (ProviderDefinition definition in ProviderCatalog.All)
        {
            snapshot.ProviderCredentials.TryGetValue(definition.ProviderId, out ProviderCredentials? credentials);
            credentials ??= new ProviderCredentials { ProviderId = definition.ProviderId };

            await SaveSecretAsync(GetApiKeySecretName(definition.ProviderId), credentials.ApiKey, cancellationToken);
            await SaveSecretAsync(GetProvisioningSecretName(definition.ProviderId), credentials.ProvisioningKey, cancellationToken);
        }
    }

    private async Task SaveSecretAsync(string key, string value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            await _secretStore.DeleteAsync(key, cancellationToken);
            return;
        }

        await _secretStore.SaveAsync(key, value.Trim(), cancellationToken);
    }

    private static string GetApiKeySecretName(string providerId) => $"api:{providerId}";

    private static string GetProvisioningSecretName(string providerId) => $"provisioning:{providerId}";
}
