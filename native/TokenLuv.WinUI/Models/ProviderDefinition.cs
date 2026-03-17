namespace TokenLuv.WinUI.Models;

public sealed class ProviderDefinition
{
    public required string ProviderId { get; init; }
    public required string DisplayName { get; init; }
    public required string ApiKeyPlaceholder { get; init; }
    public string ApiKeyLabel { get; init; } = "API key";
    public string ConnectedAuthLabel { get; init; } = "Use connected account";
    public string? ProvisioningKeyLabel { get; init; }
    public string? ProvisioningKeyPlaceholder { get; init; }
    public string? Note { get; init; }
    public string? AuthButtonLabel { get; init; }
    public string? AuthCommand { get; init; }
    public string? AuthArguments { get; init; }
    public string? OpenButtonLabel { get; init; }
    public string? OpenUrl { get; init; }
    public bool SupportsConnectedAuth { get; init; }

    public bool HasProvisioningKey => !string.IsNullOrWhiteSpace(ProvisioningKeyLabel);
    public bool HasAuthAction => !string.IsNullOrWhiteSpace(AuthButtonLabel)
        && (!string.IsNullOrWhiteSpace(AuthCommand) || !string.IsNullOrWhiteSpace(OpenUrl));
    public bool HasOpenAction => !string.IsNullOrWhiteSpace(OpenButtonLabel) && !string.IsNullOrWhiteSpace(OpenUrl);
}
