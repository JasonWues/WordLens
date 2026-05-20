using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WordLens.Models;

namespace WordLens.Services.Implementations;

public sealed class UpdateService
{
    public const string RepositoryOwner = "JasonWues";
    public const string RepositoryName = "WordLens";
    public const string RepositoryUrl = "https://github.com/JasonWues/WordLens";

    private static readonly Uri LatestReleaseApiUri =
        new($"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest");

    private readonly ProxyAwareHttpClientFactory _httpClientFactory;
    private readonly ISettingsService _settingsService;

    public UpdateService(ProxyAwareHttpClientFactory httpClientFactory, ISettingsService settingsService)
    {
        _httpClientFactory = httpClientFactory;
        _settingsService = settingsService;
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync();
        using var httpClient = _httpClientFactory.CreateClient(settings.Proxy);
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("WordLens/1.0");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return CreateNoReleaseResult();

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync(
            stream,
            SourceGenerationContext.Default.GitHubReleaseResponse,
            cancellationToken);

        if (release == null || string.IsNullOrWhiteSpace(release.TagName))
            return CreateNoReleaseResult();

        var currentVersion = GetCurrentVersion();
        var latestVersion = ParseVersion(release.TagName);

        return new UpdateCheckResult
        {
            IsUpdateAvailable = latestVersion > currentVersion,
            CurrentVersion = FormatVersion(currentVersion),
            LatestVersion = release.TagName,
            ReleaseName = string.IsNullOrWhiteSpace(release.Name) ? release.TagName : release.Name,
            ReleaseUrl = release.HtmlUrl,
            ReleaseNotes = release.Body ?? string.Empty,
            PublishedAt = release.PublishedAt
        };
    }

    public static Version GetCurrentVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
    }

    private static UpdateCheckResult CreateNoReleaseResult()
    {
        var currentVersion = GetCurrentVersion();
        return new UpdateCheckResult
        {
            CurrentVersion = FormatVersion(currentVersion),
            LatestVersion = string.Empty
        };
    }

    private static Version ParseVersion(string value)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
            normalized = normalized[1..];

        var suffixIndex = normalized.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
            normalized = normalized[..suffixIndex];

        return Version.TryParse(normalized, out var version)
            ? NormalizeVersion(version)
            : new Version(0, 0, 0);
    }

    private static Version NormalizeVersion(Version version)
    {
        return new Version(
            Math.Max(version.Major, 0),
            Math.Max(version.Minor, 0),
            Math.Max(version.Build, 0),
            Math.Max(version.Revision, 0));
    }

    private static string FormatVersion(Version version)
    {
        var normalized = NormalizeVersion(version);
        return $"v{normalized.Major}.{normalized.Minor}.{normalized.Build}";
    }
}
