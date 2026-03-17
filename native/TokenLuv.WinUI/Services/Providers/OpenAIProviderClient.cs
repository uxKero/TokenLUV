using System.Text.Json;
using TokenLuv.WinUI.Models;

namespace TokenLuv.WinUI.Services.Providers;

public sealed class OpenAIProviderClient : ProviderClientBase
{
    private static readonly string[] ModelPriority = ["o3", "o1", "gpt-4o", "gpt-4-turbo", "gpt-4", "gpt-3.5"];
    private static readonly Dictionary<string, (double Input, double Output)> Pricing = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gpt-4o"] = (2.50, 10.00),
        ["gpt-4o-mini"] = (0.15, 0.60),
        ["o1"] = (15.00, 60.00),
        ["o1-mini"] = (3.00, 12.00),
        ["o3"] = (10.00, 40.00),
        ["o3-mini"] = (1.10, 4.40),
        ["gpt-4-turbo"] = (10.00, 30.00),
        ["gpt-4"] = (30.00, 60.00),
        ["gpt-3.5-turbo"] = (0.50, 1.50)
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
        double? monthlyCost = !string.IsNullOrWhiteSpace(credentials.ProvisioningKey)
            ? await TryGetEstimatedOrgCostAsync(credentials.ProvisioningKey, cancellationToken)
            : null;

        string compactLine1 = $"5h {primary.UsedPercent:0}% used";
        string compactLine2 = BuildCompactDetail("weekly", secondary?.UsedPercent, primary.ResetAt, balance);
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

        metrics.Add(new ProviderDetailMetric
        {
            Title = "Cost",
            Summary = monthlyCost is null ? "org estimate unavailable" : $"{FormatUsd(monthlyCost.Value)} this month",
            RightLabel = monthlyCost is null ? "add org key" : "estimated",
            Footer = monthlyCost is null
                ? "For a cost line, add a legacy org key with usage.read access."
                : "Estimated from OpenAI organization usage."
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
        if (!string.IsNullOrWhiteSpace(credentials.ProvisioningKey))
        {
            used = await TryGetEstimatedOrgCostAsync(credentials.ProvisioningKey, cancellationToken);
        }

        string compactLine1 = used is null ? "validated only" : $"{FormatUsd(used.Value)} spent";
        string compactLine2 = used is null
            ? "add org key for cost"
            : "legacy platform estimate";

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
            detailMetrics:
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
                    Footer = used is null ? "Add an org key with usage.read access." : "Estimated from OpenAI organization usage."
                }
            ],
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

    private static string BuildCompactDetail(string label, double? percent, DateTimeOffset? resetAt, double? balance)
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

        string? resetLabel = DescribeReset(resetAt);
        if (!string.IsNullOrWhiteSpace(resetLabel))
        {
            parts.Add(resetLabel);
        }

        return parts.Count == 0 ? "live codex usage" : string.Join(" · ", parts);
    }

    private async Task<double?> TryGetEstimatedOrgCostAsync(string orgKey, CancellationToken cancellationToken)
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

        Dictionary<string, (double Input, double Output)> totals = new(StringComparer.OrdinalIgnoreCase);
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
                totals.TryGetValue(model, out (double Input, double Output) current);
                totals[model] = (current.Input + input, current.Output + output);
            }
        }

        double totalCost = 0;
        foreach ((string model, (double Input, double Output) tokens) in totals)
        {
            (double inputPrice, double outputPrice) = ResolvePricing(model);
            totalCost += (tokens.Input / 1_000_000d) * inputPrice;
            totalCost += (tokens.Output / 1_000_000d) * outputPrice;
        }

        return totalCost;
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

    private static (double Input, double Output) ResolvePricing(string model)
    {
        foreach ((string key, (double Input, double Output) pricing) in Pricing)
        {
            if (model.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            {
                return pricing;
            }
        }

        return Pricing["gpt-4o"];
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

    private sealed class CodexWindowSnapshot
    {
        public required double UsedPercent { get; init; }
        public DateTimeOffset? ResetAt { get; init; }
    }
}
