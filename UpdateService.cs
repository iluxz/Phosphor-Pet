using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace PhosphorPet;

internal sealed record AvailableUpdate(Version Version, string Tag, Uri InstallerUri, long InstallerSize);

internal static class UpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/iluxz/Phosphor-Pet/releases/latest";
    private const string InstallerAssetName = "PhosphorPet-Setup.exe";
    private static readonly HttpClient Client = CreateHttpClient();

    public static async Task<AvailableUpdate?> FindAvailableUpdateAsync()
    {
        using var response = await Client.GetAsync(LatestReleaseUrl);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        var release = await response.Content.ReadFromJsonAsync<GitHubRelease>()
            ?? throw new InvalidOperationException("GitHub returned an empty release response.");

        if (!Version.TryParse(release.TagName.TrimStart('v', 'V'), out var latestVersion))
            throw new InvalidOperationException($"GitHub release tag '{release.TagName}' is not a valid version.");

        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version
            ?? throw new InvalidOperationException("Could not determine the installed app version.");
        var installedVersion = new Version(
            currentVersion.Major,
            currentVersion.Minor,
            Math.Max(currentVersion.Build, 0));
        if (latestVersion <= installedVersion)
            return null;

        var asset = release.Assets.FirstOrDefault(
            candidate => string.Equals(candidate.Name, InstallerAssetName, StringComparison.Ordinal));
        if (asset is null ||
            !Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var installerUri) ||
            installerUri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(installerUri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The latest release does not contain a valid {InstallerAssetName} download.");
        }

        return new AvailableUpdate(latestVersion, release.TagName, installerUri, asset.Size);
    }

    public static async Task<string> DownloadInstallerAsync(AvailableUpdate update)
    {
        var installerPath = Path.Combine(
            Path.GetTempPath(),
            $"PhosphorPet-Setup-{Guid.NewGuid():N}.exe");
        using var response = await Client.GetAsync(
            update.InstallerUri,
            HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var output = new FileStream(
            installerPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true);
        await response.Content.CopyToAsync(output);
        if (output.Length != update.InstallerSize)
            throw new InvalidOperationException("The downloaded installer size does not match the published release asset.");

        return installerPath;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PhosphorPet-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = "";

        [JsonPropertyName("assets")]
        public GitHubAsset[] Assets { get; init; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = "";

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; init; } = "";

        [JsonPropertyName("size")]
        public long Size { get; init; }
    }
}
