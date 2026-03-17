using TokenLuv.WinUI.Models;
using TokenLuv.WinUI.Services.Settings;

namespace TokenLuv.WinUI.Services.Providers;

public sealed class ProviderMonitorService
{
    private readonly AppSettingsService _settingsService;
    private readonly IReadOnlyDictionary<string, IProviderClient> _clients;

    public ProviderMonitorService(AppSettingsService settingsService)
    {
        _settingsService = settingsService;
        _clients = new Dictionary<string, IProviderClient>(StringComparer.OrdinalIgnoreCase)
        {
            ["openrouter"] = new OpenRouterProviderClient(),
            ["openai"] = new OpenAIProviderClient(),
            ["anthropic"] = new AnthropicProviderClient(),
            ["gemini"] = new GeminiProviderClient(),
            ["antigravity"] = new AntigravityProviderClient(),
            ["xai"] = new XaiProviderClient()
        };
    }

    public IReadOnlyList<ProviderDefinition> Definitions => ProviderCatalog.All;

    public Task<AppSettingsSnapshot> LoadSettingsAsync(CancellationToken cancellationToken = default) =>
        _settingsService.LoadAsync(cancellationToken);

    public Task SaveSettingsAsync(AppSettingsSnapshot snapshot, CancellationToken cancellationToken = default) =>
        _settingsService.SaveAsync(snapshot, cancellationToken);

    public async Task<(IReadOnlyList<ProviderSnapshot> Providers, DateTimeOffset RefreshedAt)> RefreshAsync(CancellationToken cancellationToken = default)
    {
        AppSettingsSnapshot settings = await _settingsService.LoadAsync(cancellationToken);
        List<ProviderSnapshot> providers = [];
        DateTimeOffset refreshedAt = DateTimeOffset.Now;

        foreach (ProviderDefinition definition in ProviderCatalog.All)
        {
            if (!_clients.TryGetValue(definition.ProviderId, out IProviderClient? client))
            {
                continue;
            }

            settings.ProviderCredentials.TryGetValue(definition.ProviderId, out ProviderCredentials? credentials);
            credentials ??= new ProviderCredentials { ProviderId = definition.ProviderId };
            ProviderSnapshot snapshot = await client.FetchAsync(credentials, cancellationToken);
            providers.Add(snapshot);
        }

        return (providers, refreshedAt);
    }
}
