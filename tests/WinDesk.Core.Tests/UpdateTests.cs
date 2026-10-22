using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using WinDesk.Core;

internal static class UpdateTests
{
    public static async Task Run(Action<bool, string> check, string directory)
    {
        byte[] bytes = Encoding.UTF8.GetBytes("An installer payload used only by tests; never executed.");
        string digest = Convert.ToHexString(SHA256.HashData(bytes));
        string json = $$"""
        {"tag_name":"v0.7.7","draft":false,"prerelease":false,"assets":[{
        "name":"WinDesk-Setup-0.7.7-x64.exe","state":"uploaded","size":{{bytes.Length}},
        "digest":"sha256:{{digest}}","browser_download_url":"https://github.com/LogicalTR/WinDesk/releases/download/v0.7.7/WinDesk-Setup-0.7.7-x64.exe"}]}
        """;
        string Change(Action<JsonObject> mutate) { var node = JsonNode.Parse(json)!.AsObject(); mutate(node); return node.ToJsonString(); }
        var update = ReleaseUpdates.Parse(json, new Version(0, 7, 6, 0))!;
        check(update.Version == new Version(0, 7, 7), "A newer stable release selects the exact x64 installer");
        check(ReleaseUpdates.Parse(json, new Version(0, 7, 7, 0)) is null && ReleaseUpdates.Parse(json, new Version(0, 8, 0)) is null,
            "Equal/future local versions never downgrade, including four-part assembly versions");
        check(ReleaseUpdates.Parse(Change(n => n["draft"] = true), new Version(0, 7, 6)) is null
            && ReleaseUpdates.Parse(Change(n => n["prerelease"] = true), new Version(0, 7, 6)) is null, "Drafts and prereleases are not offered");
        foreach (var mutate in new Action<JsonObject>[] {
            n => n["tag_name"] = "v0.7.8-rc.1", n => n["tag_name"] = "../../0.7.8",
            n => n["assets"] = new JsonArray(),
            n => n["assets"]!.AsArray().Add(n["assets"]![0]!.DeepClone()),
            n => n["assets"]![0]!["name"] = "WinDesk-0.7.7-win-x64.exe",
            n => n["assets"]![0]!["state"] = "starter",
            n => n["assets"]![0]!["digest"] = "sha256:invalid",
            n => n["assets"]![0]!["size"] = 0,
            n => n["assets"]![0]!["size"] = 1024L * 1024 * 1024,
            n => n["assets"]![0]!["browser_download_url"] = "https://example.com/installer.exe",
            n => n["assets"]![0]!["browser_download_url"] = "http://github.com/LogicalTR/WinDesk/releases/download/v0.7.7/WinDesk-Setup-0.7.7-x64.exe"
        }) {
            bool rejected = false;
            try { ReleaseUpdates.Parse(Change(mutate), new Version(0, 7, 6)); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "Invalid release metadata is rejected before download");
        }
        using (var client = new HttpClient(new Handler((request, token) => {
            check(request.RequestUri!.AbsoluteUri == ReleaseUpdates.LatestApi && request.Headers.Authorization is null
                && request.Headers.UserAgent.Count > 0, "Update check uses the fixed public endpoint without credentials");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        }))) {
            check((await new ReleaseUpdates(client).CheckAsync(new Version(0, 7, 6)))!.Version == update.Version, "HTTP release metadata is parsed successfully");
        }
        using (var client = new HttpClient(new Handler((_, _) => new HttpResponseMessage(HttpStatusCode.Forbidden)))) {
            bool rejected = false;
            try { await new ReleaseUpdates(client).CheckAsync(new Version(0, 7, 6)); } catch (HttpRequestException) { rejected = true; }
            check(rejected, "Rate-limit and HTTP errors do not appear as 'up to date'");
        }
        HttpClient DownloadClient(byte[] payload) => new(new Handler((_, _) => {
            var content = new StreamContent(new MemoryStream(payload));
            content.Headers.ContentLength = update.Size;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }));
        string good = Path.Combine(directory, "good-update");
        using (var client = DownloadClient(bytes)) {
            string file = await new ReleaseUpdates(client).DownloadAsync(update, good);
            check(File.ReadAllBytes(file).SequenceEqual(bytes) && Directory.GetFiles(good).Length == 1,
                "Verified download becomes a final installer only after the full SHA-256 matches");
            bool rejected = false;
            try { await new ReleaseUpdates(client).DownloadAsync(update, good); } catch (IOException) { rejected = true; }
            check(rejected && File.ReadAllBytes(file).SequenceEqual(bytes) && Directory.GetFiles(good).Length == 1,
                "An existing installer is never overwritten and failed duplicate downloads leave no partial file");
            using (var locked = ReleaseUpdates.OpenVerifiedInstaller(update, file)) {
                bool protectedFromWrite = false;
                try { File.WriteAllBytes(file, bytes.Reverse().ToArray()); } catch (IOException) { protectedFromWrite = true; }
                check(protectedFromWrite, "Verified installer stays read-locked until launch");
            }
            File.WriteAllBytes(file, bytes.Reverse().ToArray());
            rejected = false;
            try { using var locked = ReleaseUpdates.OpenVerifiedInstaller(update, file); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "An installer modified after download is rejected before launch");
        }
        int index = 0;
        foreach (byte[] payload in new[] { bytes.Reverse().ToArray(), bytes[..^1], bytes.Concat(new byte[] { 1 }).ToArray() }) {
            string bad = Path.Combine(directory, "bad-update-" + index++);
            using var client = DownloadClient(payload);
            bool rejected = false;
            try { await new ReleaseUpdates(client).DownloadAsync(update, bad); } catch (InvalidDataException) { rejected = true; }
            check(rejected && !Directory.GetFiles(bad).Any(), "Corrupt, truncated or oversized downloads leave no executable or partial file");
        }
        using (var cancelled = new CancellationTokenSource())
        using (var client = new HttpClient(new Handler((_, _) => {
            cancelled.Cancel(); return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        }))) {
            string target = Path.Combine(directory, "cancelled-update");
            bool rejected = false;
            try { await new ReleaseUpdates(client).DownloadAsync(update, target, cancellationToken: cancelled.Token); } catch (OperationCanceledException) { rejected = true; }
            check(rejected && !Directory.GetFiles(target).Any(), "Cancellation never leaves a runnable update behind");
        }
        using (var client = DownloadClient(bytes)) {
            bool rejected = false;
            try { await new ReleaseUpdates(client).DownloadAsync(update with { FileName = "..\\escape.exe" }, directory); } catch (InvalidDataException) { rejected = true; }
            check(rejected, "A forged download descriptor cannot escape the download directory");
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var response = respond(request, token); response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}
