using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WinDesk.Core;

/// <summary>Opt-in installer download, always resolved from ByBrowser's latest stable release.</summary>
public sealed class ByBrowserDownloads(HttpClient http)
{
    public const string RepositoryUrl = "https://github.com/LogicalTR/ByBrowser-Releases";
    public const string LatestApi = "https://api.github.com/repos/LogicalTR/ByBrowser-Releases/releases/latest";
    public const string LatestRelease = RepositoryUrl + "/releases/latest";
    private const long MaxInstallerSize = 512L * 1024 * 1024;
    private static GitHubReleaseInstaller Client(HttpClient http) => new(http, RepositoryUrl, LatestApi, "ByBrowser", MaxInstallerSize);

    public static ReleaseUpdate? Parse(string json) => Client(null!).Parse(json, null);
    public async Task<ReleaseUpdate?> CheckAsync(CancellationToken cancellationToken = default)
    {
        try { return await Client(http).CheckAsync(null, cancellationToken).ConfigureAwait(false); }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
        {
            // Public release downloads have no dependency on the anonymous REST API quota.
            return await CheckReleaseAssetsAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ReleaseUpdate> CheckReleaseAssetsAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var latest = await SendAsync(HttpMethod.Head, LatestRelease, timeout.Token).ConfigureAwait(false);
        string releaseUrl = latest.RequestMessage?.RequestUri?.AbsoluteUri ?? "";
        var tag = Regex.Match(releaseUrl, "\\A" + Regex.Escape(RepositoryUrl) + @"/releases/tag/v(?<version>(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*))\z",
            RegexOptions.CultureInvariant);
        if (!tag.Success || !Version.TryParse(tag.Groups["version"].Value, out var version))
            throw new InvalidDataException("Kararlı ByBrowser sürümünün bağlantısı doğrulanamadı.");

        // Pin every subsequent request to this tag, even if Latest changes mid-download.
        string name = $"ByBrowser-Setup-{version}-x64.exe";
        string downloadUrl = $"{RepositoryUrl}/releases/download/v{version}/{name}";
        using var checksum = await SendAsync(HttpMethod.Get, downloadUrl + ".sha256", timeout.Token).ConfigureAwait(false);
        await checksum.Content.LoadIntoBufferAsync(4096, timeout.Token).ConfigureAwait(false);
        string checksumText = await checksum.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
        var digest = Regex.Match(checksumText.Trim(), @"\A(?<hash>[a-fA-F0-9]{64})[ \t]+\*?" + Regex.Escape(name) + @"\z",
            RegexOptions.CultureInvariant);
        if (!digest.Success)
            throw new InvalidDataException("ByBrowser kurucusunun SHA-256 doğrulama bilgisi geçerli değil.");

        using var installer = await SendAsync(HttpMethod.Head, downloadUrl, timeout.Token).ConfigureAwait(false);
        if (installer.Content.Headers.ContentLength is not long size || size <= 0 || size > MaxInstallerSize)
            throw new InvalidDataException("ByBrowser kurucusunun boyutu doğrulanamadı.");
        return new ReleaseUpdate(version, new Uri(releaseUrl), new Uri(downloadUrl), name, size, digest.Groups["hash"].Value);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.UserAgent.ParseAdd("WinDesk-Installer");
        var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        try
        {
            response.EnsureSuccessStatusCode();
            if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps)
                throw new InvalidDataException("ByBrowser indirme bağlantısı güvenli değil.");
            return response;
        }
        catch { response.Dispose(); throw; }
    }

    public Task<string> DownloadAsync(ReleaseUpdate release, string directory, IProgress<double>? progress = null, CancellationToken cancellationToken = default) =>
        Client(http).DownloadAsync(release, directory, progress, cancellationToken);
    public static FileStream OpenVerifiedInstaller(ReleaseUpdate release, string path) => Client(null!).OpenVerifiedInstaller(release, path);
}
