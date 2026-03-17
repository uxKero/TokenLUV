using System.Text.Json;
using TokenLuv.WinUI.Models;

namespace TokenLuv.WinUI.Services.Providers;

public sealed class AnthropicProviderClient : ProviderClientBase
{
    public override string ProviderId => "anthropic";
    public override string DisplayName => "Anthropic";
    public override string Description => "Explicit Claude account or legacy API keys.";
    public override ProviderDataQuality DefaultQuality => ProviderDataQuality.ValidatedOnly;

    public override async Task<ProviderSnapshot> FetchAsync(ProviderCredentials credentials, CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        Exception? oauthError = null;

        if (credentials.UseConnectedAuth && TryLoadClaudeOAuth(out ClaudeOAuthSnapshot? oauth))
        {
            try
            {
                return await FetchClaudeOAuthUsageAsync(oauth!, credentials, cancellationToken);
            }
            catch (Exception ex)
            {
                oauthError = ex;
            }
        }

        if (credentials.UseConnectedAuth && string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            return CreateSnapshot(
                ProviderDataQuality.ValidatedOnly,
                UsageUnit.Unknown,
                now,
                footnote: oauthError is null
                    ? "Run Claude auth login from settings, finish browser sign-in, then refresh TokenLUV."
                    : "Claude auth is enabled but live usage could not be read just now.",
                compactLine1Override: oauthError is null ? "connect Claude" : "Claude unavailable",
                compactLine2Override: oauthError is null ? "browser auth required" : "retry from settings",
                primaryValueOverride: oauthError is null ? "connect Claude" : "Claude unavailable",
                secondaryValueOverride: oauthError is null ? "browser auth required" : "retry from settings",
                progressPercentOverride: 0,
                detailMetrics:
                [
                    new ProviderDetailMetric
                    {
                        Title = "Claude account",
                        Summary = oauthError is null ? "not connected yet" : "could not read usage",
                        RightLabel = oauthError is null ? "required" : "retry",
                        Footer = oauthError is null
                            ? "Use Settings > Claude auth login, finish the browser flow, then keep 'Use Claude account' enabled."
                            : "The Claude account is enabled, but TokenLUV could not read live usage right now."
                    }
                ],
                usageDashboardUrl: "https://claude.ai/settings/usage",
                statusPageUrl: "https://status.anthropic.com/");
        }

        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            return oauthError is null
                ? CreateNoKeySnapshot(now)
                : CreateErrorSnapshot(now, UsageUnit.Unknown);
        }

        try
        {
            return await FetchLegacyApiSnapshotAsync(credentials, cancellationToken);
        }
        catch
        {
            return oauthError is null
                ? CreateErrorSnapshot(now, UsageUnit.Usd)
                : CreateErrorSnapshot(now, UsageUnit.Unknown);
        }
    }

    private async Task<ProviderSnapshot> FetchClaudeOAuthUsageAsync(ClaudeOAuthSnapshot oauth, ProviderCredentials credentials, CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, "https://api.anthropic.com/api/oauth/usage", oauth.AccessToken);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
        request.Headers.TryAddWithoutValidation("User-Agent", "TokenLUV");

        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return CreateErrorSnapshot(now, UsageUnit.Unknown);
        }

        using JsonDocument json = await ReadJsonAsync(response, cancellationToken);
        ClaudeWindowSnapshot? primary = TryParseClaudeWindow(json.RootElement, "five_hour");
        ClaudeWindowSnapshot? weekly = TryParseClaudeWindow(json.RootElement, "seven_day");
        ClaudeWindowSnapshot? sonnet = TryParseClaudeWindow(json.RootElement, "seven_day_sonnet")
            ?? TryParseClaudeWindow(json.RootElement, "seven_day_opus");
        ClaudeExtraUsageSnapshot? extraUsage = TryParseExtraUsage(json.RootElement);
        AnthropicAdminUsageSummary? adminUsage = !string.IsNullOrWhiteSpace(credentials.ProvisioningKey)
            ? await TryGetAdminUsageSummaryAsync(credentials.ProvisioningKey, cancellationToken)
            : null;
        double? monthlyCost = adminUsage?.MonthlyCostUsd;

        if (primary is null)
        {
            return CreateErrorSnapshot(now, UsageUnit.Unknown);
        }

        string compactLine1 = $"5h {primary.UsedPercent:0}% used";
        string compactLine2 = BuildCompactDetail(weekly, sonnet, extraUsage, primary.ResetAt);
        List<ProviderDetailMetric> metrics =
        [
            CreateWindowMetric("Session", primary.UsedPercent, primary.ResetAt, "5-hour Claude usage window")
        ];

        if (weekly is not null)
        {
            metrics.Add(CreateWindowMetric("Weekly", weekly.UsedPercent, weekly.ResetAt, "7-day Claude usage window"));
        }

        if (sonnet is not null)
        {
            metrics.Add(CreateWindowMetric("Sonnet", sonnet.UsedPercent, sonnet.ResetAt, "Model family window"));
        }

        if (extraUsage is not null)
        {
            double extraPercent = extraUsage.LimitUsd <= 0 ? 0 : (extraUsage.UsedUsd / extraUsage.LimitUsd) * 100d;
            metrics.Add(new ProviderDetailMetric
            {
                Title = "Extra usage",
                Percent = extraPercent,
                Summary = $"{FormatUsd(extraUsage.UsedUsd)} / {FormatUsd(extraUsage.LimitUsd)}",
                RightLabel = $"{Math.Clamp(extraPercent, 0, 100):0}% used",
                Footer = "Monthly extra-usage budget"
            });
        }

        ProviderDetailMetric? cacheMetric = CreateCacheMetric(adminUsage);
        if (cacheMetric is not null)
        {
            metrics.Add(cacheMetric);
        }

        metrics.Add(new ProviderDetailMetric
        {
            Title = "Cost",
            Summary = monthlyCost is null ? "admin estimate unavailable" : $"{FormatUsd(monthlyCost.Value)} this month",
            RightLabel = monthlyCost is null ? "add admin key" : "admin api",
            Footer = monthlyCost is null
                ? "Add an Anthropic admin key for a monthly cost line."
                : "Monthly cost report from Anthropic admin API."
        });

        return CreateSnapshot(
            ProviderDataQuality.Real,
            UsageUnit.Unknown,
            now,
            used: primary.UsedPercent,
            limit: 100,
            model: oauth.RateLimitTier,
            footnote: "Connected Claude account + Anthropic OAuth usage API.",
            compactLine1Override: compactLine1,
            compactLine2Override: compactLine2,
            primaryValueOverride: compactLine1,
            secondaryValueOverride: compactLine2,
            progressPercentOverride: primary.UsedPercent,
            detailMetrics: metrics,
            usageDashboardUrl: "https://claude.ai/settings/usage",
            statusPageUrl: "https://status.anthropic.com/");
    }

    private async Task<ProviderSnapshot> FetchLegacyApiSnapshotAsync(ProviderCredentials credentials, CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.Now;

        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, "https://api.anthropic.com/v1/models");
        request.Headers.Add("x-api-key", credentials.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return CreateErrorSnapshot(now, UsageUnit.Usd);
        }

        if (string.IsNullOrWhiteSpace(credentials.ProvisioningKey))
        {
            return CreateSnapshot(
                ProviderDataQuality.ValidatedOnly,
                UsageUnit.Unknown,
                now,
                footnote: "Inference key verified. Real usage needs a connected Claude account or an admin cost key.",
                compactLine1Override: "key active",
                compactLine2Override: "Claude auth recommended",
                primaryValueOverride: "key active",
                secondaryValueOverride: "Claude auth recommended",
                progressPercentOverride: 16,
                detailMetrics:
                [
                    new ProviderDetailMetric
                    {
                        Title = "API key",
                        Summary = "validated",
                        RightLabel = "no live usage",
                        Footer = "Use the connected Claude account for session and weekly usage."
                    }
                ],
                usageDashboardUrl: "https://claude.ai/settings/usage",
                statusPageUrl: "https://status.anthropic.com/");
        }

        AnthropicAdminUsageSummary? adminUsage = await TryGetAdminUsageSummaryAsync(credentials.ProvisioningKey, cancellationToken);
        double? totalUsd = adminUsage?.MonthlyCostUsd;
        List<ProviderDetailMetric> metrics =
        [
            new ProviderDetailMetric
            {
                Title = "Cost",
                Summary = totalUsd is null ? "could not read cost" : $"{FormatUsd(totalUsd.Value)} this month",
                RightLabel = totalUsd is null ? "admin error" : "admin api",
                Footer = "Admin cost report only. Connected Claude auth is still better for session and weekly windows."
            }
        ];

        ProviderDetailMetric? cacheMetric = CreateCacheMetric(adminUsage);
        if (cacheMetric is not null)
        {
            metrics.Add(cacheMetric);
        }

        return CreateSnapshot(
            ProviderDataQuality.Real,
            UsageUnit.Usd,
            now,
            totalUsd,
            null,
            footnote: "Monthly cost report from Anthropic admin API.",
            compactLine1Override: totalUsd is null ? "admin key error" : $"{FormatUsd(totalUsd.Value)} spent",
            compactLine2Override: totalUsd is null ? "could not read cost" : "monthly admin report",
            primaryValueOverride: totalUsd is null ? "admin key error" : $"{FormatUsd(totalUsd.Value)} spent",
            secondaryValueOverride: totalUsd is null ? "could not read cost" : "monthly admin report",
            progressPercentOverride: totalUsd is null ? 16 : (totalUsd > 0 ? 24 : 0),
            detailMetrics: metrics,
            usageDashboardUrl: "https://claude.ai/settings/usage",
            statusPageUrl: "https://status.anthropic.com/");
    }

    private static bool TryLoadClaudeOAuth(out ClaudeOAuthSnapshot? snapshot)
    {
        snapshot = null;
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string credentialsPath = Path.Combine(userProfile, ".claude", ".credentials.json");
        if (!File.Exists(credentialsPath))
        {
            return false;
        }

        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(credentialsPath));
        if (!json.RootElement.TryGetProperty("claudeAiOauth", out JsonElement oauthElement) || oauthElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        string? accessToken = GetString(oauthElement, "accessToken");
        double? expiresAtMs = GetDouble(oauthElement, "expiresAt");
        string[] scopes = oauthElement.TryGetProperty("scopes", out JsonElement scopesElement) && scopesElement.ValueKind == JsonValueKind.Array
            ? scopesElement.EnumerateArray().Select(scope => scope.GetString() ?? string.Empty).Where(scope => !string.IsNullOrWhiteSpace(scope)).ToArray()
            : [];

        if (string.IsNullOrWhiteSpace(accessToken) || expiresAtMs is null)
        {
            return false;
        }

        DateTimeOffset expiresAt = DateTimeOffset.FromUnixTimeMilliseconds((long)expiresAtMs.Value);
        if (DateTimeOffset.UtcNow >= expiresAt || !scopes.Contains("user:profile", StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        snapshot = new ClaudeOAuthSnapshot
        {
            AccessToken = accessToken,
            RateLimitTier = GetString(oauthElement, "rateLimitTier")
        };
        return true;
    }

    private async Task<AnthropicAdminUsageSummary?> TryGetAdminUsageSummaryAsync(string provisioningKey, CancellationToken cancellationToken)
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        DateTimeOffset start = new(new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc));
        string end = utcNow.AddDays(1).ToString("O");
        string costUrl =
            "https://api.anthropic.com/v1/organizations/cost_report" +
            $"?starting_at={Uri.EscapeDataString(start.ToString("O"))}" +
            $"&ending_at={Uri.EscapeDataString(end)}" +
            "&bucket_width=1d";

        using HttpRequestMessage costRequest = CreateRequest(HttpMethod.Get, costUrl);
        costRequest.Headers.Add("x-api-key", provisioningKey);
        costRequest.Headers.Add("anthropic-version", "2023-06-01");

        using HttpResponseMessage costResponse = await HttpClient.SendAsync(costRequest, cancellationToken);
        if (!costResponse.IsSuccessStatusCode)
        {
            return null;
        }

        using JsonDocument costJson = await ReadJsonAsync(costResponse, cancellationToken);
        double totalCents = 0;
        if (costJson.RootElement.TryGetProperty("data", out JsonElement buckets) && buckets.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement bucket in buckets.EnumerateArray())
            {
                if (!bucket.TryGetProperty("results", out JsonElement results) || results.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement result in results.EnumerateArray())
                {
                    totalCents += GetDouble(result, "amount") ?? 0;
                }
            }
        }

        string usageUrl =
            "https://api.anthropic.com/v1/organizations/usage_report/messages" +
            $"?starting_at={Uri.EscapeDataString(start.ToString("O"))}" +
            $"&ending_at={Uri.EscapeDataString(end)}" +
            "&bucket_width=1d";

        using HttpRequestMessage usageRequest = CreateRequest(HttpMethod.Get, usageUrl);
        usageRequest.Headers.Add("x-api-key", provisioningKey);
        usageRequest.Headers.Add("anthropic-version", "2023-06-01");

        using HttpResponseMessage usageResponse = await HttpClient.SendAsync(usageRequest, cancellationToken);
        if (!usageResponse.IsSuccessStatusCode)
        {
            return new AnthropicAdminUsageSummary
            {
                MonthlyCostUsd = totalCents / 100d
            };
        }

        using JsonDocument usageJson = await ReadJsonAsync(usageResponse, cancellationToken);
        double uncachedInputTokens = 0;
        double cacheReadInputTokens = 0;
        double cacheCreationInputTokens = 0;

        if (usageJson.RootElement.TryGetProperty("data", out JsonElement usageBuckets) && usageBuckets.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement bucket in usageBuckets.EnumerateArray())
            {
                if (!bucket.TryGetProperty("results", out JsonElement results) || results.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement result in results.EnumerateArray())
                {
                    uncachedInputTokens += GetDouble(result, "uncached_input_tokens") ?? GetDouble(result, "input_tokens") ?? 0;
                    cacheReadInputTokens += GetDouble(result, "cache_read_input_tokens") ?? 0;

                    if (result.TryGetProperty("cache_creation", out JsonElement cacheCreation) && cacheCreation.ValueKind == JsonValueKind.Object)
                    {
                        cacheCreationInputTokens += GetDouble(cacheCreation, "ephemeral_1h_input_tokens") ?? 0;
                        cacheCreationInputTokens += GetDouble(cacheCreation, "ephemeral_5m_input_tokens") ?? 0;
                    }

                    cacheCreationInputTokens += GetDouble(result, "cache_creation_input_tokens") ?? 0;
                }
            }
        }

        double denominator = uncachedInputTokens + cacheReadInputTokens;
        double? cacheHitRatePercent = denominator <= 0 ? null : (cacheReadInputTokens / denominator) * 100d;
        return new AnthropicAdminUsageSummary
        {
            MonthlyCostUsd = totalCents / 100d,
            UncachedInputTokens = uncachedInputTokens,
            CacheReadInputTokens = cacheReadInputTokens,
            CacheCreationInputTokens = cacheCreationInputTokens,
            CacheHitRatePercent = cacheHitRatePercent
        };
    }

    private static ClaudeWindowSnapshot? TryParseClaudeWindow(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement windowElement) || windowElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        double? utilization = GetDouble(windowElement, "utilization");
        if (utilization is null)
        {
            return null;
        }

        DateTimeOffset? resetAt = null;
        string? resetString = GetString(windowElement, "resets_at");
        if (!string.IsNullOrWhiteSpace(resetString) && DateTimeOffset.TryParse(resetString, out DateTimeOffset parsed))
        {
            resetAt = parsed;
        }

        return new ClaudeWindowSnapshot
        {
            UsedPercent = utilization.Value,
            ResetAt = resetAt
        };
    }

    private static ClaudeExtraUsageSnapshot? TryParseExtraUsage(JsonElement root)
    {
        if (!root.TryGetProperty("extra_usage", out JsonElement extraElement) || extraElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        bool enabled = extraElement.TryGetProperty("is_enabled", out JsonElement enabledElement)
            && enabledElement.ValueKind is JsonValueKind.True or JsonValueKind.False
            && enabledElement.GetBoolean();

        if (!enabled)
        {
            return null;
        }

        double? usedCredits = GetDouble(extraElement, "used_credits");
        double? monthlyLimit = GetDouble(extraElement, "monthly_limit");
        if (usedCredits is null || monthlyLimit is null)
        {
            return null;
        }

        return new ClaudeExtraUsageSnapshot
        {
            UsedUsd = usedCredits.Value / 100d,
            LimitUsd = monthlyLimit.Value / 100d
        };
    }

    private static string BuildCompactDetail(
        ClaudeWindowSnapshot? weekly,
        ClaudeWindowSnapshot? sonnet,
        ClaudeExtraUsageSnapshot? extraUsage,
        DateTimeOffset? primaryResetAt)
    {
        List<string> parts = [];

        if (weekly is not null)
        {
            parts.Add($"weekly {weekly.UsedPercent:0}%");
        }

        if (sonnet is not null)
        {
            parts.Add($"sonnet {sonnet.UsedPercent:0}%");
        }

        if (extraUsage is not null)
        {
            parts.Add($"extra {FormatUsd(extraUsage.UsedUsd)} / {FormatUsd(extraUsage.LimitUsd)}");
        }

        string? resetLabel = DescribeReset(primaryResetAt);
        if (!string.IsNullOrWhiteSpace(resetLabel))
        {
            parts.Add(resetLabel);
        }

        return parts.Count == 0 ? "live claude usage" : string.Join(" · ", parts);
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

    private static string FormatUsd(double value) => $"${value:N2}";

    private static string FormatTokens(double value)
    {
        if (value >= 1_000_000)
        {
            return $"{value / 1_000_000:0.0}M";
        }

        if (value >= 1_000)
        {
            return $"{value / 1_000:0.#}K";
        }

        return value.ToString("N0");
    }

    private static ProviderDetailMetric? CreateCacheMetric(AnthropicAdminUsageSummary? summary)
    {
        if (summary?.CacheReadInputTokens is null || summary.CacheReadInputTokens <= 0)
        {
            return null;
        }

        double hitRate = Math.Clamp(summary.CacheHitRatePercent ?? 0, 0, 100);
        string footer = summary.CacheCreationInputTokens > 0
            ? $"Admin messages report. Cache writes: {FormatTokens(summary.CacheCreationInputTokens.Value)} tokens."
            : "Admin messages report.";

        return new ProviderDetailMetric
        {
            Title = "Input cache hit",
            Percent = hitRate,
            PercentLabelOverride = $"{hitRate:0}% cached",
            Summary = $"{FormatTokens(summary.CacheReadInputTokens.Value)} cached",
            RightLabel = $"{FormatTokens(summary.UncachedInputTokens ?? 0)} uncached",
            Footer = footer
        };
    }

    private static ProviderDetailMetric CreateWindowMetric(string title, double percent, DateTimeOffset? resetAt, string footer)
    {
        return new ProviderDetailMetric
        {
            Title = title,
            Percent = percent,
            Summary = $"{percent:0}% used",
            RightLabel = DescribeReset(resetAt) ?? "live",
            Footer = footer
        };
    }

    private sealed class ClaudeOAuthSnapshot
    {
        public required string AccessToken { get; init; }
        public string? RateLimitTier { get; init; }
    }

    private sealed class ClaudeWindowSnapshot
    {
        public required double UsedPercent { get; init; }
        public DateTimeOffset? ResetAt { get; init; }
    }

    private sealed class ClaudeExtraUsageSnapshot
    {
        public required double UsedUsd { get; init; }
        public required double LimitUsd { get; init; }
    }

    private sealed class AnthropicAdminUsageSummary
    {
        public required double MonthlyCostUsd { get; init; }
        public double? UncachedInputTokens { get; init; }
        public double? CacheReadInputTokens { get; init; }
        public double? CacheCreationInputTokens { get; init; }
        public double? CacheHitRatePercent { get; init; }
    }
}
