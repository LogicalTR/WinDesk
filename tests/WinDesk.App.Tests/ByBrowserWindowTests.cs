using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WinDesk.App;
using WinDesk.Core;

internal static class ByBrowserWindowTests
{
    internal static async Task RunAsync(Action<bool, string> check, string directory)
    {
        byte[] bytes = [1, 3, 5, 7];
        string digest = Convert.ToHexString(SHA256.HashData(bytes));
        string metadata = $$"""
        {"tag_name":"v2.23.0","draft":false,"prerelease":false,"assets":[{
        "name":"ByBrowser-Setup-2.23.0-x64.exe","state":"uploaded","size":4,"digest":"sha256:{{digest}}",
        "browser_download_url":"https://github.com/LogicalTR/ByBrowser-Releases/releases/download/v2.23.0/ByBrowser-Setup-2.23.0-x64.exe"}]}
        """;
        int checks = 0, downloads = 0, launches = 0;
        bool fail = true, corrupt = false;
        string? installed = null;
        using var client = new HttpClient(new Handler(request => {
            if (request.RequestUri!.AbsoluteUri == ByBrowserDownloads.LatestApi) { checks++; return new(HttpStatusCode.OK) { Content = new StringContent(metadata) }; }
            downloads++; return new(fail ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = new ByteArrayContent(corrupt ? [7, 5, 3, 1] : bytes) };
        }));
        string target = Path.Combine(directory, "bybrowser-window");
        var window = new ByBrowserDownloadWindow(new(client), target, path => { launches++; installed = path; });
        static StackPanel Body(ByBrowserDownloadWindow w) => (StackPanel)((ScrollViewer)w.Content).Content;
        static Button Action(ByBrowserDownloadWindow w) => Body(w).Children.OfType<WrapPanel>().Single().Children.OfType<Button>().First();
        static bool Status(ByBrowserDownloadWindow w, string text) => Body(w).Children.OfType<TextBlock>().Any(t => t.Text.Contains(text));
        check(checks == 0 && downloads == 0, "Constructing the optional download window does not start network activity");
        await window.DownloadAsync();
        check(checks == 1 && downloads == 1 && launches == 0 && Action(window).IsEnabled && Status(window, "indirilemedi"),
            "ByBrowser HTTP failure allows retry without launching an installer");
        fail = false; await window.DownloadAsync(); await Dispatcher.Yield(DispatcherPriority.Background);
        check(checks == 2 && downloads == 2 && launches == 1 && Status(window, "kurulum sihirbazı açıldı"),
            "Retry resolves latest again, downloads and automatically launches verified bytes; late progress cannot overwrite completion");
        await window.InstallAsync();
        check(launches == 2 && installed is not null && File.ReadAllBytes(installed).SequenceEqual(bytes) && downloads == 2,
            "Reopening the installer revalidates bytes without a browser or second download");
        File.WriteAllBytes(installed!, [7, 5, 3, 1]); await window.InstallAsync();
        check(launches == 2 && (string)Action(window).Content == "Yeniden dene", "Tampering after download is blocked by the install action");
        corrupt = true; int fileCount = Directory.GetFiles(target, "*", SearchOption.AllDirectories).Length;
        await window.DownloadAsync();
        check(launches == 2 && Status(window, "indirilemedi") && Directory.GetFiles(target, "*", SearchOption.AllDirectories).Length == fileCount,
            "A corrupt direct download never auto-launches or leaves a partial installer");
        window.Close();

        using var waiting = new HttpClient(new WaitingHandler(metadata));
        var cancelling = new ByBrowserDownloadWindow(new(waiting), Path.Combine(directory, "bybrowser-cancel"), _ => throw new Exception("Unexpected installer launch"));
        Task pending = cancelling.DownloadAsync();
        while (!Directory.Exists(Path.Combine(directory, "bybrowser-cancel"))) await Task.Yield();
        cancelling.Cancel(); await pending;
        check(Status(cancelling, "iptal edildi") && !Directory.GetFiles(Path.Combine(directory, "bybrowser-cancel"), "*", SearchOption.AllDirectories).Any(),
            "Cancelling the actual download restores retry and leaves no installer or partial file");
        bool closed = false; cancelling.Closed += (_, _) => closed = true;
        pending = cancelling.DownloadAsync(); cancelling.Close(); await pending;
        check(closed, "Closing during a download waits for cancellation before closing the helper");
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { token.ThrowIfCancellationRequested(); var response = respond(request); response.RequestMessage = request; return Task.FromResult(response); }
    }
    private sealed class WaitingHandler(string metadata) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.AbsoluteUri == ByBrowserDownloads.LatestApi)
                return new(HttpStatusCode.OK) { Content = new StringContent(metadata), RequestMessage = request };
            await Task.Delay(Timeout.Infinite, token); throw new InvalidOperationException();
        }
    }
}
