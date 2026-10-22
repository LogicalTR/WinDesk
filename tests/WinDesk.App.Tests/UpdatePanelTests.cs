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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using WinDesk.App;
using WinDesk.Core;

internal static class UpdatePanelTests
{
    // No Application.Run, Window.Show, desktop attachment, mutex, installer or real network.
    internal static void Run(Action<bool, string> check, string output)
    {
        // A WinDesk.App instance queues its startup even without Application.Run.
        // Use a plain WPF Application so the real app/mutex/data are never touched.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var xaml = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "AppStyles.xaml"));
        XNamespace ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var resources = new XElement(ns + "ResourceDictionary", xaml.Root!.Attributes().Where(a => a.IsNamespaceDeclaration),
            xaml.Root.Element(ns + "Application.Resources")!.Elements());
        app.Resources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(resources.ToString());
        Exception? failure = null;
        Dispatcher.CurrentDispatcher.InvokeAsync(async () => {
            try { await TestAsync(check, output); }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        Dispatcher.Run();
        if (failure is not null) throw failure;
    }

    private static async Task TestAsync(Action<bool, string> check, string output)
    {
        Directory.CreateDirectory(output);
        await ByBrowserWindowTests.RunAsync(check, output);
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        string sha = Convert.ToHexString(SHA256.HashData(bytes));
        string metadata = $$"""
        {"tag_name":"v99.0.0","draft":false,"prerelease":false,"assets":[{"name":"WinDesk-Setup-99.0.0-x64.exe","state":"uploaded","size":5,"digest":"sha256:{{sha}}","browser_download_url":"https://github.com/LogicalTR/WinDesk/releases/download/v99.0.0/WinDesk-Setup-99.0.0-x64.exe"}]}
        """;
        bool consent = false, failDownload = true;
        int launches = 0, downloads = 0;
        using var client = new HttpClient(new Handler(request => {
            if (request.RequestUri!.AbsoluteUri == ReleaseUpdates.LatestApi) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(metadata) };
            downloads++;
            return new HttpResponseMessage(failDownload ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        }));
        string? installed = null;
        var panel = new UpdatePanel(new ReleaseUpdates(client), (update, file) => {
            using var verified = ReleaseUpdates.OpenVerifiedInstaller(update, file);
            check(File.Exists(file), "Installation receives a verified local file"); installed = file; launches++;
        }, _ => consent, Path.Combine(output, "downloads"));
        Button Action() => panel.Children.OfType<WrapPanel>().Single().Children.OfType<Button>().First();
        await panel.RunAsync();
        check(downloads == 0 && launches == 0 && (string)Action().Content == "Güncellemeyi indir", "Checking never downloads or starts an installer");
        await panel.RunAsync();
        check(launches == 0 && Action().IsEnabled && (string)Action().Content == "Güncellemeyi indir", "Download failure restores retry without closing the app");
        failDownload = false;
        await panel.RunAsync();
        check(launches == 0 && (string)Action().Content == "Kurulumu başlat", "Declining installation retains the verified download and keeps the app running");
        int priorDownloads = downloads;
        consent = true; await panel.RunAsync();
        check(launches == 1 && downloads == priorDownloads, "Later consent installs the verified file without downloading again");
        File.WriteAllBytes(installed!, [5, 4, 3, 2, 1]); await panel.RunAsync();
        check(launches == 1 && (string)Action().Content == "Güncellemeyi indir", "An update integrity failure stays in the UI and offers a fresh download");

        using var cancellationClient = new HttpClient(new WaitingHandler());
        var cancelPanel = new UpdatePanel(new ReleaseUpdates(cancellationClient), (_, _) => throw new Exception("Unexpected launch"), _ => true, output);
        Task pending = cancelPanel.RunAsync(); cancelPanel.Cancel(); await pending;
        check(cancelPanel.Children.OfType<TextBlock>().Any(t => t.Text == "İşlem iptal edildi."), "Cancelling a pending check restores the UI without installation");

        // Render actual WPF controls off-screen for layout review, without a window.
        var border = new Border { Width = 450, Padding = new Thickness(26), Background = new SolidColorBrush(Color.FromRgb(23, 31, 44)), Child = panel };
        TextElementForeground(panel);
        border.Measure(new Size(450, double.PositiveInfinity)); border.Arrange(new Rect(border.DesiredSize)); border.UpdateLayout();
        check(Action().ActualWidth > 100 && border.ActualHeight < 300, "Update actions fit the settings panel width");
        var bitmap = new RenderTargetBitmap(450, (int)Math.Ceiling(border.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(border);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, "update-panel.png")); png.Save(stream);
    }

    private static void TextElementForeground(DependencyObject panel) => System.Windows.Documents.TextElement.SetForeground(panel, new SolidColorBrush(Color.FromRgb(237, 241, 247)));
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { var result = response(request); result.RequestMessage = request; return Task.FromResult(result); }
    }
    private sealed class WaitingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { await Task.Delay(Timeout.Infinite, token); throw new InvalidOperationException(); }
    }
}
