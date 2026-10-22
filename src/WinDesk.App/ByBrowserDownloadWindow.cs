using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WinDesk.Core;

namespace WinDesk.App;

internal sealed class ByBrowserDownloadWindow : Window
{
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly ByBrowserDownloads _downloads;
    private readonly string _directory;
    private readonly Action<string> _launch;
    private readonly TextBlock _status = new() { Text = "En güncel sürüm bulunuyor…", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 18, 0, 12) };
    private readonly TextBlock _location = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 12, 0, 0) };
    private readonly ProgressBar _progress = new() { Height = 8, Maximum = 1 };
    private readonly Button _action = new() { Content = "Yeniden dene", IsEnabled = false, Margin = new Thickness(0, 0, 8, 0) };
    private readonly Button _folder = new() { Content = "Dosyayı göster", Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 8, 0) };
    private readonly Button _cancel = new() { Content = "İptal" };
    private CancellationTokenSource? _operation;
    private ReleaseUpdate? _release;
    private string? _installer;
    private bool _started, _closeRequested;

    internal ByBrowserDownloadWindow(ByBrowserDownloads? downloads = null, string? directory = null, Action<string>? launch = null)
    {
        _downloads = downloads ?? new ByBrowserDownloads(Http);
        _directory = directory ?? Path.Combine(UserDataLocation.DirectoryPath, "Downloads", "ByBrowser");
        _launch = launch ?? (path => {
            using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = false })
                ?? throw new InvalidOperationException("Kurulum başlatılamadı.");
        });
        Title = "WinDesk · ByBrowser indir";
        Width = 540; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        MaxHeight = Math.Max(300, SystemParameters.WorkArea.Height - 32);
        Background = ManagerWindow.Brush("#171F2C"); Foreground = ManagerWindow.Brush("#EDF1F7");
        _location.Foreground = ManagerWindow.Brush("#A4AFC1");
        var body = new StackPanel { Margin = new Thickness(26) };
        body.Children.Add(new TextBlock { Text = "ByBrowser'ı indir", FontSize = 23, FontWeight = FontWeights.SemiBold });
        body.Children.Add(new TextBlock { Text = "En güncel kararlı sürüm indirilir. Dosya doğrulanınca ByBrowser kurulum sihirbazı otomatik açılır.",
            TextWrapping = TextWrapping.Wrap, Foreground = ManagerWindow.Brush("#A4AFC1"), Margin = new Thickness(0, 6, 0, 0) });
        body.Children.Add(_status); body.Children.Add(_progress); body.Children.Add(_location);
        var buttons = new WrapPanel { Margin = new Thickness(0, 20, 0, 0) };
        buttons.Children.Add(_action); buttons.Children.Add(_folder); buttons.Children.Add(_cancel); body.Children.Add(buttons);
        Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _action.Click += async (_, _) => { if (_installer is null) await DownloadAsync(); else await InstallAsync(); };
        _cancel.Click += (_, _) => { if (_operation is not null) Cancel(); else Close(); };
        _folder.Click += (_, _) => {
            try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_installer}\"") { UseShellExecute = true }); }
            catch (Win32Exception ex) { _status.Text = "Dosya konumu açılamadı. " + ex.Message; }
        };
        ContentRendered += async (_, _) => { if (!_started) { _started = true; await DownloadAsync(); } };
        Closing += (_, e) => {
            if (_operation is null) return;
            // Let the download remove its partial file before this process exits.
            e.Cancel = true; _closeRequested = true; Cancel();
        };
    }

    internal void Cancel() => _operation?.Cancel();

    internal async Task DownloadAsync()
    {
        if (_operation is not null) return;
        using var operation = new CancellationTokenSource(); _operation = operation;
        _installer = null; _release = null;
        _action.IsEnabled = false; _folder.Visibility = Visibility.Collapsed; _cancel.Content = "İptal";
        _location.Text = ""; _status.Text = "En güncel sürüm bulunuyor…";
        _progress.IsIndeterminate = true; _progress.Value = 0;
        try
        {
            _release = await _downloads.CheckAsync(operation.Token)
                ?? throw new InvalidDataException("İndirilebilecek kararlı bir ByBrowser sürümü bulunamadı.");
            _progress.IsIndeterminate = false;
            string prefix = $"ByBrowser {_release.Version} indiriliyor";
            _status.Text = $"{prefix}… ({_release.Size / 1048576.0:0.0} MB)";
            _installer = await _downloads.DownloadAsync(_release, Path.Combine(_directory, Guid.NewGuid().ToString("N")),
                new Progress<double>(fraction => {
                    if (_operation != operation || operation.IsCancellationRequested || _installer is not null) return;
                    _progress.Value = fraction;
                    _status.Text = fraction >= 1 ? "İndirilen dosya doğrulanıyor…" : $"{prefix}… %{fraction * 100:0}";
                }), operation.Token);
            _progress.Value = 1;
            _status.Text = $"ByBrowser {_release.Version} indirildi ve doğrulandı. Kurulum açılıyor…";
            _location.Text = "Kaydedilen dosya: " + _installer;
            _folder.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        { _status.Text = operation.IsCancellationRequested ? "İndirme iptal edildi." : "Bağlantı zaman aşımına uğradı. Yeniden deneyebilirsin."; }
        catch (Exception ex) when (IsDownloadError(ex))
        { _status.Text = "ByBrowser indirilemedi. Yeniden deneyebilirsin. " + ex.Message; }
        finally
        {
            _operation = null; _progress.IsIndeterminate = false; _action.IsEnabled = true;
            _action.Content = _installer is null ? "Yeniden dene" : "Kurulumu başlat"; _cancel.Content = "Kapat";
            if (_closeRequested) Close();
        }
        if (!_closeRequested && !operation.IsCancellationRequested && _installer is not null)
            await InstallAsync();
    }

    internal async Task InstallAsync()
    {
        if (_operation is not null || _release is null || _installer is null) return;
        using var operation = new CancellationTokenSource(); _operation = operation;
        _action.IsEnabled = false;
        try
        {
            // Recheck downloaded bytes off the UI thread and keep them locked until launch.
            using var verified = await Task.Run(() => ByBrowserDownloads.OpenVerifiedInstaller(_release, _installer));
            operation.Token.ThrowIfCancellationRequested();
            _launch(_installer);
            _status.Text = "ByBrowser kurulum sihirbazı açıldı. Bu pencereyi kapatabilirsin.";
            _action.Content = "Kurulumu yeniden aç";
        }
        catch (OperationCanceledException) { _status.Text = "Kurulum başlatılmadı."; }
        catch (Exception ex) when (IsDownloadError(ex))
        {
            _installer = null; _folder.Visibility = Visibility.Collapsed; _location.Text = "";
            _action.Content = "Yeniden dene"; _status.Text = "Kurulum başlatılamadı. Yeniden indirebilirsin. " + ex.Message;
        }
        finally { _operation = null; _action.IsEnabled = true; if (_closeRequested) Close(); }
    }

    private static bool IsDownloadError(Exception ex) => ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException
        or System.Text.Json.JsonException or InvalidOperationException or System.Collections.Generic.KeyNotFoundException or Win32Exception or FormatException;
}
