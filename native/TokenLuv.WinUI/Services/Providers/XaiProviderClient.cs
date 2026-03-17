using System.Text.Json;
using TokenLuv.WinUI.Models;

namespace TokenLuv.WinUI.Services.Providers;

public sealed class XaiProviderClient : ProviderClientBase
{
    private static readonly string[] ModelPriority = ["grok-3", "grok-2", "grok-1", "grok"];

    public override string ProviderId => "xai";
    public override string DisplayName => "xAI";
    public override string Description => "Model validation only. No public usage API exists.";
    public override ProviderDataQuality DefaultQuality => ProviderDataQuality.Unsupported;

    public override async Task<ProviderSnapshot> FetchAsync(ProviderCredentials credentials, CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        if (string.IsNullOrWhiteSpace(credentials.ApiKey))
        {
            return CreateNoKeySnapshot(now);
        }

        try
        {
            using HttpRequestMessage request = CreateRequest(HttpMethod.Get, "https://api.x.ai/v1/models", credentials.ApiKey);
            using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return CreateErrorSnapshot(now, UsageUnit.Unknown);
            }

            using JsonDocument json = await ReadJsonAsync(response, cancellationToken);
            string? model = PickBestModel(json.RootElement);
            return CreateSnapshot(
                ProviderDataQuality.Unsupported,
                UsageUnit.Unknown,
                now,
                model: model,
                footnote: "No public xAI usage API.",
                detailMetrics:
                [
                    new ProviderDetailMetric
                    {
                        Title = "API key",
                        Summary = "validated",
                        RightLabel = model ?? "no model detected",
                        Footer = "xAI does not expose public usage windows, so TokenLUV can only validate the key today."
                    }
                ],
                usageDashboardUrl: "https://console.x.ai/",
                statusPageUrl: "https://status.x.ai/");
        }
        catch
        {
            return CreateErrorSnapshot(now, UsageUnit.Unknown);
        }
    }

    private static string? PickBestModel(JsonElement root)
    {
        if (!root.TryGetProperty("data", out JsonElement models) || models.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        List<string> ids = [];
        foreach (JsonElement model in models.EnumerateArray())
        {
            string? id = GetString(model, "id");
            if (!string.IsNullOrWhiteSpace(id) && id.StartsWith("grok", StringComparison.OrdinalIgnoreCase))
            {
                ids.Add(id);
            }
        }

        return ids
            .OrderBy(id => GetPriority(id))
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
        int lastDash = id.LastIndexOf('-');
        if (lastDash > 0 && long.TryParse(id[(lastDash + 1)..], out _))
        {
            return id[..lastDash];
        }

        return id;
    }
}
