using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WinDesk.Core;

internal static class ByBrowserDownloadTests
{
    internal static async Task Run(Action<bool, string> check, string directory)
    {
        byte[] bytes = [10, 20, 30, 40];
        string digest = Convert.ToHexString(SHA256.HashData(bytes));
        string Metadata(string version) => $$"""
        {"tag_name":"v{{version}}","draft":false,"prerelease":false,"assets":[{
        "name":"ByBrowser-Setup-{{version}}-x64.exe","state":"uploaded","size":4,"digest":"sha256:{{digest}}",
        "browser_download_url":"https://github.com/LogicalTR/ByBrowser-Releases/releases/download/v{{version}}/ByBrowser-Setup-{{version}}-x64.exe"}]}
        """;
        string json = Metadata("2.23.0");
        var release = ByBrowserDownloads.Parse(json)!;
        check(release.FileName == "ByBrowser-Setup-2.23.0-x64.exe", "ByBrowser selects the exact x64 installer from its own repository");
        string latest = "2.23.0";
        using var client = new HttpClient(new Handler(request => {
            if (request.RequestUri!.AbsoluteUri == ByBrowserDownloads.LatestApi) {
                check(request.Headers.Authorization is null, "ByBrowser lookup sends no account credentials");
                return new(HttpStatusCode.OK) { Content = new StringContent(Metadata(latest)) };
            }
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        }));
        var downloads = new ByBrowserDownloads(client);
        check((await downloads.CheckAsync())!.Version == new Version(2, 23, 0), "ByBrowser resolves the current latest release at download time");
        latest = "3.0.1";
        check((await downloads.CheckAsync())!.Version == new Version(3, 0, 1), "A newly published ByBrowser version is found without updating WinDesk");
        foreach (var mutate in new Action<JsonObject>[] {
            n => n["tag_name"] = "v2.23.0-beta", n => n["assets"]!.AsArray().Add(n["assets"]![0]!.DeepClone()),
            n => n["assets"]![0]!["digest"] = "sha256:wrong",
            n => n["assets"]![0]!["browser_download_url"] = "https://example.com/ByBrowser.exe",
            n => n["assets"]![0]!["name"] = "../ByBrowser.exe"
        }) {
            var node = JsonNode.Parse(json)!.AsObject(); mutate(node); bool rejected = false;
            try { ByBrowserDownloads.Parse(node.ToJsonString()); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "Invalid ByBrowser metadata is rejected before any download");
        }
        var prerelease = JsonNode.Parse(json)!.AsObject(); prerelease["prerelease"] = true;
        check(ByBrowserDownloads.Parse(prerelease.ToJsonString()) is null, "ByBrowser prereleases are never downloaded");
        string file = await downloads.DownloadAsync(release, Path.Combine(directory, "bybrowser"));
        using (var verified = ByBrowserDownloads.OpenVerifiedInstaller(release, file))
            check(verified.Length == bytes.Length && File.ReadAllBytes(file).SequenceEqual(bytes), "ByBrowser downloads and verifies the full installer bytes");
        bool wrongProduct = false;
        try { using var verified = ReleaseUpdates.OpenVerifiedInstaller(release, file); } catch (InvalidDataException) { wrongProduct = true; }
        check(wrongProduct, "The WinDesk updater still refuses a ByBrowser installer descriptor");
        File.WriteAllBytes(file, [40, 30, 20, 10]);
        bool tampered = false;
        try { using var verified = ByBrowserDownloads.OpenVerifiedInstaller(release, file); } catch (InvalidDataException) { tampered = true; }
        check(tampered, "ByBrowser installer modified after download is rejected before launch");
        await RateLimitTests(check, directory, bytes, digest);
    }

    private static async Task RateLimitTests(Action<bool, string> check, string directory, byte[] bytes, string digest)
    {
        const string name = "ByBrowser-Setup-3.0.1-x64.exe";
        const string releaseUrl = ByBrowserDownloads.RepositoryUrl + "/releases/tag/v3.0.1";
        const string downloadUrl = ByBrowserDownloads.RepositoryUrl + "/releases/download/v3.0.1/" + name;
        string checksum = digest + "  " + name + "\n";
        var requests = new List<string>();
        HttpClient Client(HttpStatusCode status, string redirectedRelease = releaseUrl, string? checksumText = null, long? size = 4) =>
            new(new Handler(request => {
                string url = request.RequestUri!.AbsoluteUri;
                requests.Add(request.Method + " " + url);
                if (request.Headers.Authorization is not null || request.Headers.UserAgent.Count == 0)
                    throw new Exception("Public downloads must identify the client without account credentials.");
                if (url == ByBrowserDownloads.LatestApi) return new(status);
                if (url == ByBrowserDownloads.LatestRelease && request.Method == HttpMethod.Head)
                    return new(HttpStatusCode.OK) { RequestMessage = new HttpRequestMessage(HttpMethod.Head, redirectedRelease) };
                if (url == downloadUrl + ".sha256" && request.Method == HttpMethod.Get)
                    return new(HttpStatusCode.OK) { Content = new StringContent(checksumText ?? checksum) };
                if (url == downloadUrl && request.Method == HttpMethod.Head) {
                    // A real HEAD has headers but no installer bytes, including unknown-length responses.
                    var content = new StreamContent(Stream.Null); content.Headers.ContentLength = size;
                    return new(HttpStatusCode.OK) { Content = content };
                }
                if (url == downloadUrl && request.Method == HttpMethod.Get)
                    return new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
                throw new Exception("Unexpected fallback request: " + request.Method + " " + url);
            }));

        foreach (var status in new[] { HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests }) {
            requests.Clear();
            using var client = Client(status);
            var downloads = new ByBrowserDownloads(client);
            var release = (await downloads.CheckAsync())!;
            string file = await downloads.DownloadAsync(release, Path.Combine(directory, "fallback-" + (int)status));
            using var verified = ByBrowserDownloads.OpenVerifiedInstaller(release, file);
            check(release.Version == new Version(3, 0, 1) && release.Sha256 == digest && verified.Length == 4
                && File.ReadAllBytes(file).SequenceEqual(bytes), $"HTTP {(int)status} falls back to the current public release and downloads verified installer bytes");
            check(requests.SequenceEqual(new[] {
                "GET " + ByBrowserDownloads.LatestApi, "HEAD " + ByBrowserDownloads.LatestRelease,
                "GET " + downloadUrl + ".sha256", "HEAD " + downloadUrl, "GET " + downloadUrl
            }), "Fallback makes no API retry and pins checksum, size and installer to the resolved release");
        }

        foreach (string invalidRelease in new[] {
            releaseUrl + "-beta", releaseUrl + "?tag=v3.0.1", releaseUrl.Replace("https:", "http:"),
            releaseUrl.Replace("ByBrowser-Releases", "WinDesk"), ByBrowserDownloads.LatestRelease,
            releaseUrl.Replace("v3.0.1", "v03.0.1"), releaseUrl.Replace("v3.0.1", "v999999999999.0.1")
        }) {
            requests.Clear(); using var client = Client(HttpStatusCode.Forbidden, redirectedRelease: invalidRelease);
            bool rejected = false;
            try { await new ByBrowserDownloads(client).CheckAsync(); } catch (InvalidDataException) { rejected = true; }
            check(rejected && requests.Count == 2, "Fallback rejects an untrusted, unresolved or non-stable release before requesting assets");
        }
        foreach (string invalidChecksum in new[] {
            "invalid  " + name, digest + "  Other-Setup-3.0.1-x64.exe", digest + "  ../" + name,
            checksum + checksum, digest, digest + "  " + name + ".exe"
        }) {
            requests.Clear(); using var client = Client(HttpStatusCode.Forbidden, checksumText: invalidChecksum);
            bool rejected = false;
            try { await new ByBrowserDownloads(client).CheckAsync(); } catch (InvalidDataException) { rejected = true; }
            check(rejected && requests.Count == 3, "Fallback requires a single SHA-256 entry for the exact versioned installer");
        }
        foreach (long? invalidSize in new long?[] { null, 0, 513L * 1024 * 1024 }) {
            using var client = Client(HttpStatusCode.Forbidden, size: invalidSize);
            bool rejected = false;
            try { await new ByBrowserDownloads(client).CheckAsync(); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "Fallback rejects missing, empty or oversized installer lengths");
        }
        using (var client = Client(HttpStatusCode.Forbidden, checksumText: new string('a', 4097))) {
            bool rejected = false;
            try { await new ByBrowserDownloads(client).CheckAsync(); } catch (HttpRequestException) { rejected = true; }
            check(rejected, "Fallback bounds checksum response allocation");
        }
        requests.Clear();
        using (var client = Client(HttpStatusCode.NotFound)) {
            bool rejected = false;
            try { await new ByBrowserDownloads(client).CheckAsync(); }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound) { rejected = true; }
            check(rejected && requests.Count == 1, "Unrelated API failures do not trigger the rate-limit fallback");
        }
        using (var client = new HttpClient(new WaitingFallbackHandler()))
        using (var cancellation = new CancellationTokenSource()) {
            Task pending = new ByBrowserDownloads(client).CheckAsync(cancellation.Token);
            cancellation.Cancel(); bool cancelled = false;
            try { await pending; } catch (OperationCanceledException) { cancelled = true; }
            check(cancelled, "Cancellation interrupts the fallback release lookup");
        }
    }

    private sealed class WaitingFallbackHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.AbsoluteUri == ByBrowserDownloads.LatestApi)
                return new(HttpStatusCode.Forbidden) { RequestMessage = request };
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException();
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { token.ThrowIfCancellationRequested(); var response = respond(request); response.RequestMessage ??= request; return Task.FromResult(response); }
    }
}
