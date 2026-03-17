using TokenLuv.WinUI.Models;

namespace TokenLuv.WinUI.Services.Providers;

public interface IProviderClient
{
    string ProviderId { get; }
    string DisplayName { get; }
    string Description { get; }
    ProviderDataQuality DefaultQuality { get; }

    Task<ProviderSnapshot> FetchAsync(ProviderCredentials credentials, CancellationToken cancellationToken = default);
}
