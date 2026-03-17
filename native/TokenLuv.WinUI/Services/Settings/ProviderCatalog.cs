using TokenLuv.WinUI.Models;

namespace TokenLuv.WinUI.Services.Settings;

public static class ProviderCatalog
{
    public static IReadOnlyList<ProviderDefinition> All { get; } =
    [
        new ProviderDefinition
        {
            ProviderId = "openrouter",
            DisplayName = "OpenRouter",
            ApiKeyPlaceholder = "sk-or-v1-...",
            ApiKeyLabel = "API token",
            AuthButtonLabel = "Get token",
            OpenUrl = "https://openrouter.ai/settings/keys",
            Note = "Paste one API token. TokenLUV reads /credits and /key directly."
        },
        new ProviderDefinition
        {
            ProviderId = "openai",
            DisplayName = "OpenAI",
            SupportsConnectedAuth = true,
            ConnectedAuthLabel = "Use Codex account",
            ApiKeyPlaceholder = "sk-... (optional legacy key)",
            ApiKeyLabel = "Legacy API key",
            ProvisioningKeyLabel = "Legacy Org Key",
            ProvisioningKeyPlaceholder = "usage.read-enabled org key",
            AuthButtonLabel = "Launch Codex",
            AuthCommand = "explorer.exe",
            AuthArguments = "shell:AppsFolder\\OpenAI.Codex_2p2nqsd0c76g0!App",
            OpenButtonLabel = "Open usage",
            OpenUrl = "https://chatgpt.com/codex/settings/usage",
            Note = "Best path: launch Codex, sign in there, then enable the Codex account here. TokenLUV will not read ~/.codex/auth.json unless you enable it."
        },
        new ProviderDefinition
        {
            ProviderId = "anthropic",
            DisplayName = "Anthropic",
            SupportsConnectedAuth = true,
            ConnectedAuthLabel = "Use Claude account",
            ApiKeyPlaceholder = "sk-ant-... (optional legacy key)",
            ApiKeyLabel = "Legacy API key",
            ProvisioningKeyLabel = "Legacy Admin Key",
            ProvisioningKeyPlaceholder = "sk-ant-admin...",
            AuthButtonLabel = "Claude auth login",
            AuthCommand = "claude",
            AuthArguments = "auth login",
            OpenButtonLabel = "Open usage",
            OpenUrl = "https://claude.ai/settings/usage",
            Note = "Best path: run Claude Code auth login from here, finish browser sign-in, then enable the connected Claude account."
        },
        new ProviderDefinition
        {
            ProviderId = "gemini",
            DisplayName = "Gemini",
            SupportsConnectedAuth = true,
            ConnectedAuthLabel = "Use Gemini CLI auth",
            ApiKeyPlaceholder = "AIza... (optional legacy key)",
            ApiKeyLabel = "Legacy API key",
            AuthButtonLabel = "Launch Gemini CLI",
            AuthCommand = "gemini",
            OpenButtonLabel = "Open docs",
            OpenUrl = "https://github.com/google-gemini/gemini-cli",
            Note = "Gemini CLI triggers Google sign-in. After login, TokenLUV can reuse and refresh the CLI OAuth token to read live quota windows."
        },
        new ProviderDefinition
        {
            ProviderId = "antigravity",
            DisplayName = "Antigravity",
            SupportsConnectedAuth = true,
            ConnectedAuthLabel = "Enable local probe",
            ApiKeyPlaceholder = "local runtime only",
            ApiKeyLabel = "Local runtime",
            AuthButtonLabel = "Launch Antigravity",
            AuthCommand = "explorer.exe",
            AuthArguments = "shell:AppsFolder\\Google.Antigravity",
            OpenUrl = "https://antigravity.ai/",
            OpenButtonLabel = "Open site",
            Note = "Antigravity has no public quota auth path here yet. TokenLUV can probe the local runtime only after you explicitly enable it."
        },
        new ProviderDefinition
        {
            ProviderId = "xai",
            DisplayName = "xAI",
            ApiKeyPlaceholder = "xai-...",
            ApiKeyLabel = "API key",
            AuthButtonLabel = "Get token",
            OpenUrl = "https://console.x.ai/",
            Note = "Model validation only for now. No public usage API exists."
        }
    ];
}
