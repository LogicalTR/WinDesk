using System;
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
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { token.ThrowIfCancellationRequested(); var response = respond(request); response.RequestMessage = request; return Task.FromResult(response); }
    }
}
