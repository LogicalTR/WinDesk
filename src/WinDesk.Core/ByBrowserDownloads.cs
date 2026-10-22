using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WinDesk.Core;

/// <summary>Opt-in installer download, always resolved from ByBrowser's latest stable release.</summary>
public sealed class ByBrowserDownloads(HttpClient http)
{
    public const string RepositoryUrl = "https://github.com/LogicalTR/ByBrowser-Releases";
    public const string LatestApi = "https://api.github.com/repos/LogicalTR/ByBrowser-Releases/releases/latest";
    private static GitHubReleaseInstaller Client(HttpClient http) => new(http, RepositoryUrl, LatestApi, "ByBrowser", 512L * 1024 * 1024);

    public static ReleaseUpdate? Parse(string json) => Client(null!).Parse(json, null);
    public Task<ReleaseUpdate?> CheckAsync(CancellationToken cancellationToken = default) => Client(http).CheckAsync(null, cancellationToken);
    public Task<string> DownloadAsync(ReleaseUpdate release, string directory, IProgress<double>? progress = null, CancellationToken cancellationToken = default) =>
        Client(http).DownloadAsync(release, directory, progress, cancellationToken);
    public static FileStream OpenVerifiedInstaller(ReleaseUpdate release, string path) => Client(null!).OpenVerifiedInstaller(release, path);
}
