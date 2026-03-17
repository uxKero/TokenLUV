using System.Net.Http.Headers;
using System.Text.Json;
using TokenLuv.WinUI.Models;

namespace TokenLuv.WinUI.Services.Providers;

public abstract class ProviderClientBase : IProviderClient
{
    protected static readonly HttpClient HttpClient = new();

    public abstract string ProviderId { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public abstract ProviderDataQuality DefaultQuality { get; }

    public abstract Task<ProviderSnapshot> FetchAsync(ProviderCredentials credentials, CancellationToken cancellationToken = default);

    protected static HttpRequestMessage CreateRequest(HttpMethod method, string url, string? bearerToken = null)
    {
        HttpRequestMessage request = new(method, url);
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return request;
    }

    protected static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    protected ProviderSnapshot CreateSnapshot(
        ProviderDataQuality quality,
        UsageUnit unit,
        DateTimeOffset refreshedAt,
        double? used = null,
        double? limit = null,
        string? model = null,
        bool hasError = false,
        string? footnote = null,
        bool isConfigured = true,
        string? compactLine1Override = null,
        string? compactLine2Override = null,
        string? primaryValueOverride = null,
        string? secondaryValueOverride = null,
        double? progressPercentOverride = null,
        IReadOnlyList<ProviderDetailMetric>? detailMetrics = null,
        string? usageDashboardUrl = null,
        string? statusPageUrl = null)
    {
        return new ProviderSnapshot
        {
            ProviderId = ProviderId,
            DisplayName = DisplayName,
            Description = Description,
            Quality = quality,
            Unit = unit,
            Used = used,
            Limit = limit,
            Model = model,
            HasError = hasError,
            IsConfigured = isConfigured,
            RefreshedAt = refreshedAt,
            Footnote = footnote ?? Description,
            CompactLine1Override = compactLine1Override,
            CompactLine2Override = compactLine2Override,
            PrimaryValueOverride = primaryValueOverride,
            SecondaryValueOverride = secondaryValueOverride,
            ProgressPercentOverride = progressPercentOverride,
            DetailMetrics = detailMetrics ?? [],
            UsageDashboardUrl = usageDashboardUrl,
            StatusPageUrl = statusPageUrl
        };
    }

    protected ProviderSnapshot CreateNoKeySnapshot(DateTimeOffset refreshedAt)
    {
        return CreateSnapshot(DefaultQuality, UsageUnit.Unknown, refreshedAt, isConfigured: false);
    }

    protected ProviderSnapshot CreateErrorSnapshot(DateTimeOffset refreshedAt, UsageUnit unit)
    {
        return CreateSnapshot(DefaultQuality, unit, refreshedAt, hasError: true);
    }

    protected static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    protected static double? GetDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String when double.TryParse(value.GetString(), out double result) => result,
            _ => null
        };
    }
}
