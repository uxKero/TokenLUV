namespace TokenLuv.WinUI.Models;

public sealed class ProviderCredentials
{
    public required string ProviderId { get; init; }
    public string ApiKey { get; init; } = string.Empty;
    public string ProvisioningKey { get; init; } = string.Empty;
    public bool UseConnectedAuth { get; init; }
}
