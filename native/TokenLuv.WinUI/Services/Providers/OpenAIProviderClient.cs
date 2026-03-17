using System.Text.Json;
using TokenLuv.WinUI.Models;

namespace TokenLuv.WinUI.Services.Providers;

public sealed class OpenAIProviderClient : ProviderClientBase
{
    private static readonly string[] ModelPriority = ["o3", "o1", "gpt-4o", "gpt-4-turbo", "gpt-4", "gpt-3.5"];
    private static readonly Dictionary<string, OpenAiPricing> Pricing = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gpt-4o"] = new(2.50, 1.25, 10.00),
        ["gpt-4o-mini"] = new(0.15, 0.075, 0.60),
        ["o1"] = new(15.00, null, 60.00),
        ["o1-mini"] = new(3.00, null, 12.00),
        ["o3"] = new(10.00, null, 40.00),
        ["o3-mini"] = new(1.10, null, 4.40),
        ["gpt-4-turbo"] = new(10.00, null, 30.00),
        ["gpt-4"] = new(30.00, null, 60.00),
        ["gpt-3.5-turbo"] = new(0.50, null, 1.50)
    };

    public override string ProviderId => "openai";
    public override string DisplayName => "OpenAI";
    public override string Description => "Explicit Codex account or legacy platform keys.";
    public override ProviderDataQuality DefaultQuality => ProviderDataQuality.Estimated;

    public override async Task<ProviderSnapshot> FetchAsync(ProviderCredentials credentials, CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        Exception? realSourceError = null;

        if (credentials.UseConnectedAuth && TryLoadCodexAuth(out CodexAuthSnapshot? codexAuth))
        {
            try
            {
                return await FetchCodexUsageAsync(codexAuth!, credentials, cancellationToken);
            }
            catch (Exception ex)
            {
                realSourceError = ex;
            }
        }

        if (credentials.UseConnectedAuth && string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            return CreateSnapshot(
                ProviderDataQuality.ValidatedOnly,
                UsageUnit.Unknown,
                now,
                footnote: realSourceError is null
                    ? "Launch Codex from settings, finish sign-in, then refresh TokenLUV."
                    : "Codex auth was enabled but the usage endpoint did not return data.",
                compactLine1Override: realSourceError is null ? "connect Codex" : "Codex unavailable",
                compactLine2Override: realSourceError is null ? "browser auth required" : "retry from settings",
                primaryValueOverride: realSourceError is null ? "connect Codex" : "Codex unavailable",
                secondaryValueOverride: realSourceError is null ? "browser auth required" : "retry from settings",
                progressPercentOverride: 0,
                detailMetrics:
                [
                    new ProviderDetailMetric
                    {
                        Title = "Codex account",
                        Summary = realSourceError is null ? "not connected yet" : "could not read usage",
                        RightLabel = realSourceError is null ? "required" : "retry",
                        Footer = realSourceError is null
                            ? "Use Settings > Launch Codex, complete login, then keep 'Use Codex account' enabled."
                            : "Codex account is enabled, but TokenLUV could not read live usage just now."
                    }
                ],
                usageDashboardUrl: "https://chatgpt.com/codex/settings/usage",
                statusPageUrl: "https://status.openai.com/");
        }

        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            return realSourceError is null
                ? CreateNoKeySnapshot(now)
                : CreateErrorSnapshot(now, UsageUnit.Unknown);
        }

        try
        {
            return await FetchLegacyPlatformEstimateAsync(credentials, cancellationToken);
        }
        catch
        {
            return realSourceError is null
                ? CreateErrorSnapshot(now, UsageUnit.Usd)
                : CreateErrorSnapshot(now, UsageUnit.Unknown);
        }
    }

    private async Task<ProviderSnapshot> FetchCodexUsageAsync(CodexAuthSnapshot auth, ProviderCredentials credentials, CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, ResolveCodexUsageUrl(), auth.AccessToken);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("User-Agent", "TokenLUV");
        if (!string.IsNullOrWhiteSpace(auth.AccountId))
        {
            request.Headers.TryAddWithoutValidation("ChatGPT-Account-Id", auth.AccountId);
        }

        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return CreateErrorSnapshot(now, UsageUnit.Unknown);
        }

        using JsonDocument json = await ReadJsonAsync(response, cancellationToken);
        CodexWindowSnapshot? primary = TryParseWindow(json.RootElement, "primary_window");
        CodexWindowSnapshot? secondary = TryParseWindow(json.RootElement, "secondary_window");
        if (json.RootElement.TryGetProperty("rate_limit", out JsonElement rateLimitElement))
        {
            primary ??= TryParseWindow(rateLimitElement, "primary_window");
            secondary ??= TryParseWindow(rateLimitElement, "secondary_window");
        }

        if (primary is null)
        {
            return CreateErrorSnapshot(now, UsageUnit.Unknown);
        }

        double? balance = TryParseCreditsBalance(json.RootElement);
        OpenAiOrgUsageSummary? orgSummary = !string.IsNullOrWhiteSpace(credentials.ProvisioningKey)
            ? await TryGetOrgUsageSummaryAsync(credentials.ProvisioningKey, cancellationToken)
            : null;

        string compactLine1 = $"5h {primary.UsedPercent:0}% used";
        string compactLine2 = BuildCompactDetail("weekly", secondary?.UsedPercent, primary.ResetAt, balance, orgSummary);
        List<ProviderDetailMetric> metrics =
        [
            CreateWindowMetric("Session", primary.UsedPercent, primary.ResetAt, "5-hour Codex usage window")
        ];

        if (secondary is not null)
        {
            metrics.Add(CreateWindowMetric("Weekly", secondary.UsedPercent, secondary.ResetAt, "7-day Codex usage window"));
        }

        if (balance is not null)
        {
            metrics.Add(new ProviderDetailMetric
            {
                Title = "Extra usage",
                Summary = $"{FormatUsd(balance.Value)} left",
                RightLabel = "credits",
                Footer = "Balance exposed by the ChatGPT Codex usage API."
            });
        }

        ProviderDetailMetric? cacheMetric = CreateCacheMetric(orgSummary);
        if (cacheMetric is not null)
        {
            metrics.Add(cacheMetric);
        }

        metrics.Add(new ProviderDetailMetric
        {
            Title = "Cost",
            Summary = orgSummary is null ? "org estimate unavailable" : $"{FormatUsd(orgSummary.EstimatedCostUsd)} this month",
            RightLabel = orgSummary is null ? "add org key" : "estimated",
            Footer = orgSummary is null
                ? "For a cost line, add a legacy org key with usage.read access."
                : "Estimated from OpenAI organization usage, including cache hits when pricing is available."
        });

        return CreateSnapshot(
            ProviderDataQuality.Real,
            UsageUnit.Unknown,
            now,
            used: primary.UsedPercent,
            limit: 100,
            model: GetString(json.RootElement, "plan_type"),
            footnote: "Connected Codex account + ChatGPT usage API.",
            compactLine1Override: compactLine1,
            compactLine2Override: compactLine2,
            primaryValueOverride: compactLine1,
            secondaryValueOverride: compactLine2,
            progressPercentOverride: primary.UsedPercent,
            detailMetrics: metrics,
            usageDashboardUrl: "https://chatgpt.com/codex/settings/usage",
            statusPageUrl: "https://status.openai.com/");
    }

    private async Task<ProviderSnapshot> FetchLegacyPlatformEstimateAsync(ProviderCredentials credentials, CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.Now;

        using HttpRequestMessage modelsRequest = CreateRequest(HttpMethod.Get, "https://api.openai.com/v1/models", credentials.ApiKey);
        using HttpResponseMessage modelsResponse = await HttpClient.SendAsync(modelsRequest, cancellationToken);
        if (!modelsResponse.IsSuccessStatusCode)
        {
            return CreateErrorSnapshot(now, UsageUnit.Usd);
        }

        using JsonDocument modelsJson = await ReadJsonAsync(modelsResponse, cancellationToken);
        string? model = PickBestModel(modelsJson.RootElement);

        double? used = null;
        OpenAiOrgUsageSummary? orgSummary = null;
        if (!string.IsNullOrWhiteSpace(credentials.ProvisioningKey))
        {
            orgSummary = await TryGetOrgUsageSummaryAsync(credentials.ProvisioningKey, cancellationToken);
            used = orgSummary?.EstimatedCostUsd;
        }

        string compactLine1 = used is null ? "validated only" : $"{FormatUsd(used.Value)} spent";
        string compactLine2 = used is null
            ? "add org key for cost"
            : "legacy platform estimate";

        List<ProviderDetailMetric> metrics =
        [
            new ProviderDetailMetric
            {
                Title = "API key",
                Summary = "validated",
                RightLabel = model ?? "unknown model",
                Footer = "This mode is only an estimate. Prefer a connected Codex account for live windows."
            },
            new ProviderDetailMetric
            {
                Title = "Cost",
                Summary = used is null ? "estimate unavailable" : $"{FormatUsd(used.Value)} this month",
                RightLabel = used is null ? "needs org key" : "estimated",
                Footer = used is null
                    ? "Add an org key with usage.read access."
                    : "Estimated from OpenAI organization usage, including cache hits when pricing is available."
            }
        ];

        ProviderDetailMetric? cacheMetric = CreateCacheMetric(orgSummary);
        if (cacheMetric is not null)
        {
            metrics.Add(cacheMetric);
        }

        return CreateSnapshot(
            ProviderDataQuality.Estimated,
            UsageUnit.Usd,
            now,
            used,
            null,
            model,
            footnote: "Estimated from OpenAI platform APIs.",
            compactLine1Override: compactLine1,
            compactLine2Override: compactLine2,
            primaryValueOverride: compactLine1,
            secondaryValueOverride: compactLine2,
            progressPercentOverride: used is null ? 14 : 34,
            detailMetrics: metrics,
            usageDashboardUrl: "https://platform.openai.com/usage",
            statusPageUrl: "https://status.openai.com/");
    }

    private static bool TryLoadCodexAuth(out CodexAuthSnapshot? auth)
    {
        auth = null;
        string? codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        string root = !string.IsNullOrWhiteSpace(codexHome)
            ? codexHome
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
        string authPath = Path.Combine(root, "auth.json");
        if (!File.Exists(authPath))
        {
            return false;
        }

        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(authPath));
        JsonElement rootElement = json.RootElement;

        string? accessToken = GetString(rootElement, "OPENAI_API_KEY");
        string? accountId = null;

        if (rootElement.TryGetProperty("tokens", out JsonElement tokensElement) && tokensElement.ValueKind == JsonValueKind.Object)
        {
            accessToken ??= GetString(tokensElement, "access_token");
            accountId = GetString(tokensElement, "account_id");
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }

        auth = new CodexAuthSnapshot
        {
            AccessToken = accessToken,
            AccountId = accountId
        };
        return true;
    }

    private static string ResolveCodexUsageUrl() => "https://chatgpt.com/backend-api/wham/usage";

    private static CodexWindowSnapshot? TryParseWindow(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement windowElement) || windowElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        double? usedPercent = GetDouble(windowElement, "used_percent");
        if (usedPercent is null)
        {
            return null;
        }

        double? resetAt = GetDouble(windowElement, "reset_at");
        return new CodexWindowSnapshot
        {
            UsedPercent = usedPercent.Value,
            ResetAt = resetAt is null ? null : DateTimeOffset.FromUnixTimeSeconds((long)resetAt.Value)
        };
    }

    private static double? TryParseCreditsBalance(JsonElement root)
    {
        if (root.TryGetProperty("credits", out JsonElement creditsElement) && creditsElement.ValueKind == JsonValueKind.Object)
        {
            return GetDouble(creditsElement, "balance");
        }

        return null;
    }

    private static string BuildCompactDetail(string label, double? percent, DateTimeOffset? resetAt, double? balance, OpenAiOrgUsageSummary? orgSummary)
    {
        List<string> parts = [];
        if (percent is not null)
        {
            parts.Add($"{label} {percent.Value:0}%");
        }

        if (balance is not null)
        {
            parts.Add($"{FormatUsd(balance.Value)} left");
        }

        if (orgSummary?.CacheHitRatePercent is > 0)
        {
            parts.Add($"cache {orgSummary.CacheHitRatePercent.Value:0}%");
        }

        string? resetLabel = DescribeReset(resetAt);
        if (!string.IsNullOrWhiteSpace(resetLabel))
        {
            parts.Add(resetLabel);
        }

        return parts.Count == 0 ? "live codex usage" : string.Join(" · ", parts);
    }

    private async Task<OpenAiOrgUsageSummary?> TryGetOrgUsageSummaryAsync(string orgKey, CancellationToken cancellationToken)
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        long startTime = new DateTimeOffset(new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)).ToUnixTimeSeconds();
        long endTime = utcNow.ToUnixTimeSeconds();
        string url = $"https://api.openai.com/v1/organization/usage/completions?start_time={startTime}&end_time={endTime}&bucket_width=1d";

        using HttpRequestMessage usageRequest = CreateRequest(HttpMethod.Get, url, orgKey);
        using HttpResponseMessage usageResponse = await HttpClient.SendAsync(usageRequest, cancellationToken);
        if (!usageResponse.IsSuccessStatusCode)
        {
            return null;
        }

        using JsonDocument usageJson = await ReadJsonAsync(usageResponse, cancellationToken);
        if (!usageJson.RootElement.TryGetProperty("data", out JsonElement buckets) || buckets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        Dictionary<string, OpenAiModelUsageTotals> totals = new(StringComparer.OrdinalIgnoreCase);
        double totalInputTokens = 0;
        double totalCachedInputTokens = 0;
        foreach (JsonElement bucket in buckets.EnumerateArray())
        {
            if (!bucket.TryGetProperty("results", out JsonElement results) || results.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement result in results.EnumerateArray())
            {
                string model = GetString(result, "model") ?? "gpt-4o";
                double input = GetDouble(result, "input_tokens") ?? 0;
                double output = GetDouble(result, "output_tokens") ?? 0;
                double cachedInput = GetCachedInputTokens(result);
                OpenAiModelUsageTotals current = totals.TryGetValue(model, out OpenAiModelUsageTotals existing)
                    ? existing
                    : default;
                totals[model] = new OpenAiModelUsageTotals(
                    current.Input + input,
                    current.CachedInput + cachedInput,
                    current.Output + output);
                totalInputTokens += input;
                totalCachedInputTokens += cachedInput;
            }
        }

        double totalCost = 0;
        foreach ((string model, OpenAiModelUsageTotals tokens) in totals)
        {
            OpenAiPricing pricing = ResolvePricing(model);
            double uncachedInputTokens = Math.Max(0, tokens.Input - tokens.CachedInput);
            totalCost += (uncachedInputTokens / 1_000_000d) * pricing.Input;
            totalCost += (tokens.CachedInput / 1_000_000d) * (pricing.CachedInput ?? pricing.Input);
            totalCost += (tokens.Output / 1_000_000d) * pricing.Output;
        }

        double totalPromptTokens = totalInputTokens + totalCachedInputTokens;
        double? cacheHitRate = totalPromptTokens <= 0 ? null : (totalCachedInputTokens / totalPromptTokens) * 100d;
        return new OpenAiOrgUsageSummary
        {
            EstimatedCostUsd = totalCost,
            InputTokens = totalInputTokens,
            CachedInputTokens = totalCachedInputTokens,
            OutputTokens = totals.Values.Sum(value => value.Output),
            CacheHitRatePercent = cacheHitRate
        };
    }

    private static string? PickBestModel(JsonElement root)
    {
        if (!root.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        List<string> ids = [];
        foreach (JsonElement model in data.EnumerateArray())
        {
            string? id = GetString(model, "id");
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }
        }

        IEnumerable<string> candidates = ids
            .Where(id => id.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase) || id.StartsWith("o1", StringComparison.OrdinalIgnoreCase) || id.StartsWith("o3", StringComparison.OrdinalIgnoreCase))
            .Where(id => !id.Contains("instruct", StringComparison.OrdinalIgnoreCase) && !id.Contains("vision", StringComparison.OrdinalIgnoreCase));

        return candidates
            .OrderBy(GetPriority)
            .ThenByDescending(id => id)
            .Select(CleanModelId)
            .FirstOrDefault();
    }

    private static int GetPriority(string id)
    {
        int index = Array.FindIndex(ModelPriority, tier => id.StartsWith(tier, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? 99 : index;
    }

    private static string CleanModelId(string id)
    {
        int trailingDateIndex = id.LastIndexOf('-');
        if (trailingDateIndex > 0 && DateOnly.TryParse(id[(trailingDateIndex + 1)..], out _))
        {
            return id[..trailingDateIndex];
        }

        return id;
    }

    private static double GetCachedInputTokens(JsonElement result)
    {
        double? direct = GetDouble(result, "input_cached_tokens") ?? GetDouble(result, "cached_input_tokens");
        if (direct is not null)
        {
            return direct.Value;
        }

        if (result.TryGetProperty("input_tokens_details", out JsonElement inputDetails) && inputDetails.ValueKind == JsonValueKind.Object)
        {
            double? cached = GetDouble(inputDetails, "cached_tokens");
            if (cached is not null)
            {
                return cached.Value;
            }
        }

        if (result.TryGetProperty("prompt_tokens_details", out JsonElement promptDetails) && promptDetails.ValueKind == JsonValueKind.Object)
        {
            double? cached = GetDouble(promptDetails, "cached_tokens");
            if (cached is not null)
            {
                return cached.Value;
            }
        }

        return 0;
    }

    private static OpenAiPricing ResolvePricing(string model)
    {
        foreach ((string key, OpenAiPricing pricing) in Pricing)
        {
            if (model.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            {
                return pricing;
            }
        }

        return Pricing["gpt-4o"];
    }

    private static ProviderDetailMetric? CreateCacheMetric(OpenAiOrgUsageSummary? summary)
    {
        if (summary?.CachedInputTokens is null || summary.CachedInputTokens <= 0)
        {
            return null;
        }

        double hitRate = Math.Clamp(summary.CacheHitRatePercent ?? 0, 0, 100);
        return new ProviderDetailMetric
        {
            Title = "Input cache hit",
            Percent = hitRate,
            PercentLabelOverride = $"{hitRate:0}% cached",
            Summary = $"{FormatCompact(summary.CachedInputTokens)} cached",
            RightLabel = $"{FormatCompact(summary.InputTokens)} uncached",
            Footer = "Monthly OpenAI org aggregate from cached input tokens."
        };
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

    private static string FormatCompact(double value)
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

    private sealed class CodexAuthSnapshot
    {
        public required string AccessToken { get; init; }
        public string? AccountId { get; init; }
    }

    private sealed class OpenAiOrgUsageSummary
    {
        public required double EstimatedCostUsd { get; init; }
        public required double InputTokens { get; init; }
        public required double CachedInputTokens { get; init; }
        public required double OutputTokens { get; init; }
        public double? CacheHitRatePercent { get; init; }
    }

    private readonly record struct OpenAiModelUsageTotals(double Input, double CachedInput, double Output);

    private readonly record struct OpenAiPricing(double Input, double? CachedInput, double Output);

    private sealed class CodexWindowSnapshot
    {
        public required double UsedPercent { get; init; }
        public DateTimeOffset? ResetAt { get; init; }
    }
}
