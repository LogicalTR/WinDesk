using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WinDesk.Core;

public sealed record ReleaseUpdate(Version Version, Uri ReleaseUrl, Uri DownloadUrl, string FileName, long Size, string Sha256);

/// <summary>WinDesk updates remain restricted to the WinDesk release repository.</summary>
public sealed class ReleaseUpdates(HttpClient http)
{
    public const string RepositoryUrl = "https://github.com/LogicalTR/WinDesk";
    public const string LatestApi = "https://api.github.com/repos/LogicalTR/WinDesk/releases/latest";
    private static GitHubReleaseInstaller Client(HttpClient http) => new(http, RepositoryUrl, LatestApi, "WinDesk", 256L * 1024 * 1024);

    public static ReleaseUpdate? Parse(string json, Version current) => Client(null!).Parse(json, current);
    public Task<ReleaseUpdate?> CheckAsync(Version current, CancellationToken cancellationToken = default) => Client(http).CheckAsync(current, cancellationToken);
    public Task<string> DownloadAsync(ReleaseUpdate update, string directory, IProgress<double>? progress = null, CancellationToken cancellationToken = default) =>
        Client(http).DownloadAsync(update, directory, progress, cancellationToken);
    public static FileStream OpenVerifiedInstaller(ReleaseUpdate update, string path) => Client(null!).OpenVerifiedInstaller(update, path);
}
