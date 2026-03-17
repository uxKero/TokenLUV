using Microsoft.UI.Xaml;

namespace TokenLuv.WinUI.Models;

public sealed class ProviderSettingsEditorItem
{
    public string ProviderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ApiKeyLabel { get; set; } = "API key";
    public string ConnectedAuthLabel { get; set; } = "Use connected account";
    public string ApiKeyPlaceholder { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ProvisioningKey { get; set; } = string.Empty;
    public bool UseConnectedAuth { get; set; }
    public bool SupportsConnectedAuth { get; set; }
    public string? ProvisioningKeyLabel { get; set; }
    public string? ProvisioningKeyPlaceholder { get; set; }
    public string? AuthButtonLabel { get; set; }
    public string? AuthCommand { get; set; }
    public string? AuthArguments { get; set; }
    public string? OpenButtonLabel { get; set; }
    public string? OpenUrl { get; set; }

    public bool HasProvisioningKey => !string.IsNullOrWhiteSpace(ProvisioningKeyLabel);
    public bool HasAuthAction => !string.IsNullOrWhiteSpace(AuthButtonLabel)
        && (!string.IsNullOrWhiteSpace(AuthCommand) || !string.IsNullOrWhiteSpace(OpenUrl));
    public bool HasOpenAction => !string.IsNullOrWhiteSpace(OpenButtonLabel) && !string.IsNullOrWhiteSpace(OpenUrl);
    public Visibility ProvisioningVisibility => HasProvisioningKey ? Visibility.Visible : Visibility.Collapsed;
    public Visibility AuthButtonVisibility => HasAuthAction ? Visibility.Visible : Visibility.Collapsed;
    public Visibility OpenButtonVisibility => HasOpenAction ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ConnectedAuthVisibility => SupportsConnectedAuth ? Visibility.Visible : Visibility.Collapsed;
}
