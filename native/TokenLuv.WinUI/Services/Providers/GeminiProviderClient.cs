using System.Text;
using System.Text.Json;
using TokenLuv.WinUI.Models;

namespace TokenLuv.WinUI.Services.Providers;

public sealed class GeminiProviderClient : ProviderClientBase
{
    private static readonly string[] ModelPriority = ["gemini-2.5-pro", "gemini-2.5-flash", "gemini-2.0-pro", "gemini-2.0-flash", "gemini-1.5-pro", "gemini-1.5-flash"];

    public override string ProviderId => "gemini";
    public override string DisplayName => "Gemini";
    public override string Description => "Explicit Gemini CLI auth or legacy API key validation.";
    public override ProviderDataQuality DefaultQuality => ProviderDataQuality.ValidatedOnly;

    public override async Task<ProviderSnapshot> FetchAsync(ProviderCredentials credentials, CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        Exception? quotaError = null;

        if (credentials.UseConnectedAuth)
        {
            GeminiOAuthSnapshot? oauth = await TryLoadGeminiCliOAuthAsync(cancellationToken);
            if (oauth is not null)
            {
                try
                {
                    return await FetchGeminiQuotaAsync(oauth, cancellationToken);
                }
                catch (Exception ex)
                {
                    quotaError = ex;
                }
            }
        }

        if (credentials.UseConnectedAuth && string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            return CreateSnapshot(
                ProviderDataQuality.ValidatedOnly,
                UsageUnit.Unknown,
                now,
                footnote: quotaError is null
                    ? "Launch Gemini CLI from settings, complete Google sign-in, then refresh TokenLUV."
                    : "Gemini CLI auth is enabled but quota could not be refreshed from Google OAuth.",
                compactLine1Override: quotaError is null ? "connect Gemini" : "quota unavailable",
                compactLine2Override: quotaError is null ? "Google OAuth required" : "retry from settings",
                primaryValueOverride: quotaError is null ? "connect Gemini" : "quota unavailable",
                secondaryValueOverride: quotaError is null ? "Google OAuth required" : "retry from settings",
                progressPercentOverride: 0,
                detailMetrics:
                [
                    new ProviderDetailMetric
                    {
                        Title = "Gemini CLI",
                        Summary = quotaError is null ? "not connected yet" : "could not refresh quota",
                        RightLabel = quotaError is null ? "required" : "retry",
                        Footer = "TokenLUV uses the same Google OAuth flow as Gemini CLI and refreshes quota tokens when possible."
                    }
                ],
                usageDashboardUrl: "https://aistudio.google.com/",
                statusPageUrl: "https://status.cloud.google.com/");
        }

        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            return quotaError is null
                ? CreateNoKeySnapshot(now)
                : CreateErrorSnapshot(now, UsageUnit.Unknown);
        }

        try
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={credentials.ApiKey}";
            using HttpRequestMessage request = CreateRequest(HttpMethod.Get, url);
            using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return CreateErrorSnapshot(now, UsageUnit.Unknown);
            }

            using JsonDocument json = await ReadJsonAsync(response, cancellationToken);
            string? model = PickBestModel(json.RootElement);
            return CreateSnapshot(
                ProviderDataQuality.ValidatedOnly,
                UsageUnit.Unknown,
                now,
                model: model,
                footnote: "Legacy Gemini API key validation only.",
                compactLine1Override: "key active",
                compactLine2Override: model is null ? "no live quota" : model,
                primaryValueOverride: "key active",
                secondaryValueOverride: "no live quota",
                progressPercentOverride: 16,
                detailMetrics:
                [
                    new ProviderDetailMetric
                    {
                        Title = "API key",
                        Summary = "validated",
                        RightLabel = model ?? "unknown model",
                        Footer = "Gemini API keys validate model access but do not expose Gemini CLI quota windows."
                    }
                ],
                usageDashboardUrl: "https://aistudio.google.com/",
                statusPageUrl: "https://status.cloud.google.com/");
        }
        catch
        {
            return CreateErrorSnapshot(now, UsageUnit.Unknown);
        }
    }

    private async Task<ProviderSnapshot> FetchGeminiQuotaAsync(GeminiOAuthSnapshot oauth, CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, "https://cloudcode-pa.googleapis.com/v1internal:retrieveUserQuota", oauth.AccessToken);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return CreateErrorSnapshot(now, UsageUnit.Unknown);
        }

        using JsonDocument json = await ReadJsonAsync(response, cancellationToken);
        List<GeminiQuotaEntry> entries = [];
        CollectQuotaEntries(json.RootElement, entries);
        GeminiQuotaEntry? primary = PickBestQuota(entries);
        GeminiQuotaEntry? secondary = entries
            .Where(entry => entry != primary)
            .OrderBy(entry => entry.RemainingFraction)
            .FirstOrDefault();

        if (primary is null)
        {
            return CreateSnapshot(
                ProviderDataQuality.ValidatedOnly,
                UsageUnit.Unknown,
                now,
                footnote: "Gemini CLI auth detected, but quota data was empty.",
                compactLine1Override: "oauth active",
                compactLine2Override: "no quota visible",
                primaryValueOverride: "oauth active",
                secondaryValueOverride: "no quota visible",
                progressPercentOverride: 16,
                detailMetrics:
                [
                    new ProviderDetailMetric
                    {
                        Title = "Gemini CLI",
                        Summary = "connected",
                        RightLabel = "no quota visible",
                        Footer = "Google OAuth is valid, but the quota payload was empty."
                    }
                ],
                usageDashboardUrl: "https://aistudio.google.com/",
                statusPageUrl: "https://status.cloud.google.com/");
        }

        double primaryUsedPercent = (1 - primary.RemainingFraction) * 100d;
        string compactLine1 = $"{primary.DisplayLabel} {primaryUsedPercent:0}% used";
        string compactLine2 = BuildQuotaDetail(secondary, primary.ResetAt);
        List<ProviderDetailMetric> metrics =
        [
            CreateQuotaMetric(primary)
        ];

        if (secondary is not null)
        {
            metrics.Add(CreateQuotaMetric(secondary));
        }

        metrics.Add(new ProviderDetailMetric
        {
            Title = "Quota policy",
            Summary = "CLI + Code Assist shared quota",
            RightLabel = oauth.AuthType,
            Footer = "Google documents that Gemini CLI and Gemini Code Assist agent mode share per-user request quotas."
        });

        return CreateSnapshot(
            ProviderDataQuality.Real,
            UsageUnit.Unknown,
            now,
            used: primaryUsedPercent,
            limit: 100,
            model: primary.ModelId,
            footnote: "Gemini CLI OAuth quota via Google quota API with token refresh support.",
            compactLine1Override: compactLine1,
            compactLine2Override: compactLine2,
            primaryValueOverride: compactLine1,
            secondaryValueOverride: compactLine2,
            progressPercentOverride: primaryUsedPercent,
            detailMetrics: metrics,
            usageDashboardUrl: "https://aistudio.google.com/",
            statusPageUrl: "https://status.cloud.google.com/");
    }

    private static async Task<GeminiOAuthSnapshot?> TryLoadGeminiCliOAuthAsync(CancellationToken cancellationToken)
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini");
        string settingsPath = Path.Combine(root, "settings.json");
        string credsPath = Path.Combine(root, "oauth_creds.json");
        if (!File.Exists(settingsPath) || !File.Exists(credsPath))
        {
            return null;
        }

        using JsonDocument settingsJson = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath, cancellationToken));
        JsonElement settingsRoot = settingsJson.RootElement;
        string? authType = GetString(settingsRoot, "authType") ?? GetString(settingsRoot, "selectedAuthType");
        if (!string.Equals(authType, "oauth-personal", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string rawCreds = await File.ReadAllTextAsync(credsPath, cancellationToken);
        using JsonDocument credsJson = JsonDocument.Parse(rawCreds);
        JsonElement credsRoot = credsJson.RootElement;

        string? accessToken = GetString(credsRoot, "access_token");
        string? refreshToken = GetString(credsRoot, "refresh_token");
        string? clientId = GetString(credsRoot, "client_id");
        string? clientSecret = GetString(credsRoot, "client_secret");
        double? expiryMs = GetDouble(credsRoot, "expiry_date");
        DateTimeOffset? expiresAt = expiryMs is null ? null : DateTimeOffset.FromUnixTimeMilliseconds((long)expiryMs.Value);

        if (!string.IsNullOrWhiteSpace(refreshToken) &&
            !string.IsNullOrWhiteSpace(clientId) &&
            !string.IsNullOrWhiteSpace(clientSecret) &&
            (string.IsNullOrWhiteSpace(accessToken) || expiresAt is null || DateTimeOffset.UtcNow >= expiresAt.Value.AddMinutes(-2)))
        {
            GoogleTokenRefreshSnapshot? refreshed = await TryRefreshAccessTokenAsync(refreshToken, clientId, clientSecret, cancellationToken);
            if (refreshed is not null)
            {
                accessToken = refreshed.AccessToken;
                expiresAt = refreshed.ExpiresAt;
            }
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        if (expiresAt is not null && DateTimeOffset.UtcNow >= expiresAt.Value)
        {
            return null;
        }

        return new GeminiOAuthSnapshot
        {
            AccessToken = accessToken,
            AuthType = authType ?? "oauth-personal"
        };
    }

    private static async Task<GoogleTokenRefreshSnapshot?> TryRefreshAccessTokenAsync(
        string refreshToken,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            })
        };

        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using JsonDocument json = await ReadJsonAsync(response, cancellationToken);
        string? accessToken = GetString(json.RootElement, "access_token");
        double? expiresInSeconds = GetDouble(json.RootElement, "expires_in");
        if (string.IsNullOrWhiteSpace(accessToken) || expiresInSeconds is null)
        {
            return null;
        }

        return new GoogleTokenRefreshSnapshot
        {
            AccessToken = accessToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds.Value)
        };
    }

    private static void CollectQuotaEntries(JsonElement element, List<GeminiQuotaEntry> entries)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (TryCreateQuotaEntry(element, out GeminiQuotaEntry? entry))
                {
                    entries.Add(entry!);
                }

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    CollectQuotaEntries(property.Value, entries);
                }
                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    CollectQuotaEntries(item, entries);
                }
                break;
        }
    }

    private static bool TryCreateQuotaEntry(JsonElement element, out GeminiQuotaEntry? entry)
    {
        entry = null;
        double? remainingFraction = GetDouble(element, "remainingFraction");
        if (remainingFraction is null)
        {
            return false;
        }

        string? modelId = GetString(element, "modelId") ?? GetString(element, "quotaId") ?? GetString(element, "name");
        string displayLabel = BuildDisplayLabel(modelId);
        DateTimeOffset? resetAt = null;
        string? resetString = GetString(element, "resetTime");
        if (!string.IsNullOrWhiteSpace(resetString) && DateTimeOffset.TryParse(resetString, out DateTimeOffset parsed))
        {
            resetAt = parsed;
        }

        entry = new GeminiQuotaEntry
        {
            ModelId = modelId,
            DisplayLabel = displayLabel,
            RemainingFraction = Math.Clamp(remainingFraction.Value, 0, 1),
            ResetAt = resetAt
        };
        return true;
    }

    private static GeminiQuotaEntry? PickBestQuota(IEnumerable<GeminiQuotaEntry> entries)
    {
        return entries
            .OrderBy(entry => GetPriority(entry.ModelId))
            .ThenBy(entry => entry.RemainingFraction)
            .FirstOrDefault();
    }

    private static string BuildQuotaDetail(GeminiQuotaEntry? secondary, DateTimeOffset? resetAt)
    {
        List<string> parts = [];
        if (secondary is not null)
        {
            double secondaryUsedPercent = (1 - secondary.RemainingFraction) * 100d;
            parts.Add($"{secondary.DisplayLabel} {secondaryUsedPercent:0}%");
        }

        string? resetLabel = DescribeReset(resetAt);
        if (!string.IsNullOrWhiteSpace(resetLabel))
        {
            parts.Add(resetLabel);
        }

        return parts.Count == 0 ? "oauth quota live" : string.Join(" · ", parts);
    }

    private static string? DescribeReset(DateTimeOffset? resetAt)
    {
        if (resetAt is null)
        {
            return null;
        }

        TimeSpan remaining = resetAt.Value - DateTimeOffset.Now;
        if (remaining.TotalMinutes <= 0)
        {
            return "resets soon";
        }

        if (remaining.TotalHours < 1)
        {
            return $"resets in {Math.Max(1, (int)Math.Round(remaining.TotalMinutes))}m";
        }

        if (remaining.TotalDays < 1)
        {
            return $"resets in {Math.Max(1, (int)remaining.TotalHours)}h";
        }

        return $"resets in {Math.Max(1, (int)remaining.TotalDays)}d";
    }

    private static string BuildDisplayLabel(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return "quota";
        }

        string lower = modelId.ToLowerInvariant();
        if (lower.Contains("pro"))
        {
            return "pro";
        }

        if (lower.Contains("flash"))
        {
            return "flash";
        }

        return "quota";
    }

    private static string? PickBestModel(JsonElement root)
    {
        if (!root.TryGetProperty("models", out JsonElement models) || models.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        List<string> ids = [];
        foreach (JsonElement model in models.EnumerateArray())
        {
            string? name = GetString(model, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                ids.Add(name.Replace("models/", string.Empty, StringComparison.OrdinalIgnoreCase));
            }
        }

        return ids
            .Where(id => id.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase))
            .OrderBy(GetPriority)
            .ThenByDescending(id => id)
            .FirstOrDefault();
    }

    private static int GetPriority(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return 999;
        }

        int index = Array.FindIndex(ModelPriority, tier => id.StartsWith(tier, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? 99 : index;
    }

    private static ProviderDetailMetric CreateQuotaMetric(GeminiQuotaEntry entry)
    {
        double usedPercent = (1 - entry.RemainingFraction) * 100d;
        return new ProviderDetailMetric
        {
            Title = entry.DisplayLabel.ToUpperInvariant(),
            Percent = usedPercent,
            Summary = $"{usedPercent:0}% used",
            RightLabel = DescribeReset(entry.ResetAt) ?? "live",
            Footer = string.IsNullOrWhiteSpace(entry.ModelId) ? "Gemini quota window" : entry.ModelId
        };
    }

    private sealed class GeminiOAuthSnapshot
    {
        public required string AccessToken { get; init; }
        public required string AuthType { get; init; }
    }

    private sealed class GoogleTokenRefreshSnapshot
    {
        public required string AccessToken { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
    }

    private sealed class GeminiQuotaEntry
    {
        public string? ModelId { get; init; }
        public required string DisplayLabel { get; init; }
        public required double RemainingFraction { get; init; }
        public DateTimeOffset? ResetAt { get; init; }
    }
}
