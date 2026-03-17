using System.Text.Json;
using TokenLuv.WinUI.Models;

namespace TokenLuv.WinUI.Services.Providers;

public sealed class OpenRouterProviderClient : ProviderClientBase
{
    public override string ProviderId => "openrouter";
    public override string DisplayName => "OpenRouter";
    public override string Description => "Credits and key quota from the OpenRouter API.";
    public override ProviderDataQuality DefaultQuality => ProviderDataQuality.Real;

    public override async Task<ProviderSnapshot> FetchAsync(ProviderCredentials credentials, CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        string apiKey = !string.IsNullOrWhiteSpace(credentials.ApiKey)
            ? credentials.ApiKey
            : credentials.ProvisioningKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return CreateNoKeySnapshot(now);
        }

        try
        {
            using HttpRequestMessage creditsRequest = CreateOpenRouterRequest("https://openrouter.ai/api/v1/credits", apiKey);
            using HttpResponseMessage creditsResponse = await HttpClient.SendAsync(creditsRequest, cancellationToken);
            if (!creditsResponse.IsSuccessStatusCode)
            {
                return CreateErrorSnapshot(now, UsageUnit.Usd);
            }

            using JsonDocument creditsJson = await ReadJsonAsync(creditsResponse, cancellationToken);
            JsonElement creditsData = creditsJson.RootElement.TryGetProperty("data", out JsonElement dataElement)
                ? dataElement
                : creditsJson.RootElement;

            double totalCredits = GetDouble(creditsData, "total_credits") ?? 0;
            double totalUsage = GetDouble(creditsData, "total_usage") ?? 0;
            double balance = Math.Max(0, totalCredits - totalUsage);

            OpenRouterKeySnapshot? keySnapshot = await TryGetKeySnapshotAsync(apiKey, cancellationToken);
            string compactLine1 = $"{FormatUsd(balance)} left";
            string compactLine2 = keySnapshot?.Describe() ?? $"{FormatUsd(totalUsage)} spent / {FormatUsd(totalCredits)}";

            return CreateSnapshot(
                ProviderDataQuality.Real,
                UsageUnit.Usd,
                now,
                totalUsage,
                totalCredits,
                footnote: "OpenRouter credits via /credits and key quota via /key.",
                compactLine1Override: compactLine1,
                compactLine2Override: compactLine2,
                primaryValueOverride: compactLine1,
                secondaryValueOverride: compactLine2,
                detailMetrics:
                [
                    new ProviderDetailMetric
                    {
                        Title = "Credits",
                        Percent = totalCredits <= 0 ? null : (totalUsage / totalCredits) * 100d,
                        Summary = $"{FormatUsd(totalUsage)} / {FormatUsd(totalCredits)}",
                        RightLabel = $"{FormatUsd(balance)} left",
                        Footer = "Total credits and usage from OpenRouter /credits."
                    },
                    new ProviderDetailMetric
                    {
                        Title = "Key quota",
                        Summary = keySnapshot?.Describe() ?? "live key",
                        RightLabel = "api",
                        Footer = "Current key usage and rate limits from OpenRouter /key."
                    }
                ],
                usageDashboardUrl: "https://openrouter.ai/settings/credits",
                statusPageUrl: "https://status.openrouter.ai/");
        }
        catch
        {
            return CreateErrorSnapshot(now, UsageUnit.Usd);
        }
    }

    private static HttpRequestMessage CreateOpenRouterRequest(string url, string apiKey)
    {
        HttpRequestMessage request = CreateRequest(HttpMethod.Get, url, apiKey);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("X-Title", "TokenLUV");
        return request;
    }

    private async Task<OpenRouterKeySnapshot?> TryGetKeySnapshotAsync(string apiKey, CancellationToken cancellationToken)
    {
        using HttpRequestMessage keyRequest = CreateOpenRouterRequest("https://openrouter.ai/api/v1/key", apiKey);
        using HttpResponseMessage keyResponse = await HttpClient.SendAsync(keyRequest, cancellationToken);
        if (!keyResponse.IsSuccessStatusCode)
        {
            return null;
        }

        using JsonDocument keyJson = await ReadJsonAsync(keyResponse, cancellationToken);
        JsonElement keyData = keyJson.RootElement.TryGetProperty("data", out JsonElement dataElement)
            ? dataElement
            : keyJson.RootElement;

        OpenRouterRateLimit? rateLimit = null;
        if (keyData.TryGetProperty("rate_limit", out JsonElement rateLimitElement) && rateLimitElement.ValueKind == JsonValueKind.Object)
        {
            rateLimit = new OpenRouterRateLimit
            {
                Requests = GetDouble(rateLimitElement, "requests"),
                Interval = GetString(rateLimitElement, "interval")
            };
        }

        return new OpenRouterKeySnapshot
        {
            Limit = GetDouble(keyData, "limit"),
            Usage = GetDouble(keyData, "usage"),
            RateLimit = rateLimit
        };
    }

    private static string FormatUsd(double value) => $"${value:N2}";

    private sealed class OpenRouterKeySnapshot
    {
        public double? Limit { get; init; }
        public double? Usage { get; init; }
        public OpenRouterRateLimit? RateLimit { get; init; }

        public string Describe()
        {
            if (Limit is > 0 && Usage is not null)
            {
                return $"key {Usage.Value:N0}/{Limit.Value:N0}";
            }

            if (RateLimit?.Requests is > 0 && !string.IsNullOrWhiteSpace(RateLimit.Interval))
            {
                return $"{RateLimit.Requests.Value:N0} req / {RateLimit.Interval}";
            }

            return "live credits";
        }
    }

    private sealed class OpenRouterRateLimit
    {
        public double? Requests { get; init; }
        public string? Interval { get; init; }
    }
}
