using System.Reflection;
using System.Text.Json;
using TokenLuv.WinUI.Models;

namespace TokenLuv.WinUI.Services.Updates;

public sealed class AppUpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/WonuSama/TokenLUV/releases/latest";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task<AppUpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        string currentVersion = GetCurrentVersion();

        using HttpRequestMessage request = new(HttpMethod.Get, LatestReleaseUrl);
        using HttpResponseMessage response = await HttpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        JsonElement root = document.RootElement;

        string? tagName = root.TryGetProperty("tag_name", out JsonElement tagElement)
            ? tagElement.GetString()
            : null;
        string? releaseUrl = root.TryGetProperty("html_url", out JsonElement urlElement)
            ? urlElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(tagName) || string.IsNullOrWhiteSpace(releaseUrl))
        {
            return null;
        }

        Version current = ParseVersion(currentVersion);
        Version latest = ParseVersion(tagName);

        if (latest <= current)
        {
            return null;
        }

        return new AppUpdateInfo(currentVersion, latest.ToString(), releaseUrl);
    }

    public static string GetCurrentVersion()
    {
        Version? version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version is null)
        {
            return "0.0.0";
        }

        return version.Build > 0
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : $"{version.Major}.{version.Minor}.0";
    }

    private static Version ParseVersion(string version)
    {
        string normalized = version.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        string[] parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return new Version(0, 0, 0);
        }

        List<string> numericParts = parts.Take(4).ToList();
        while (numericParts.Count < 3)
        {
            numericParts.Add("0");
        }

        return Version.TryParse(string.Join(".", numericParts), out Version? parsed)
            ? parsed
            : new Version(0, 0, 0);
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TokenLUV/0.1");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
