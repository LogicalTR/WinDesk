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

internal sealed class UpdatePanel : StackPanel
{
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly ReleaseUpdates _updates;
    private readonly Action<ReleaseUpdate, string> _install;
    private readonly Func<ReleaseUpdate, bool> _confirm;
    private readonly string _downloadDirectory;
    private readonly Button _action = new() { Content = "Güncellemeleri denetle", Margin = new Thickness(0, 10, 8, 0) };
    private readonly Button _cancel = new() { Content = "İptal", Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed };
    private readonly Button _notes = new() { Content = "Sürüm notları", Margin = new Thickness(0, 10, 8, 0), Visibility = Visibility.Collapsed };
    private readonly TextBlock _status = new() { Text = "Yeni sürümler GitHub'dan denetlenir. Kurulum için onayın istenir.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly ProgressBar _progress = new() { Height = 4, Maximum = 1, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 0) };
    private CancellationTokenSource? _operation;
    private ReleaseUpdate? _available;
    private string? _installer;

    internal UpdatePanel(ReleaseUpdates? updates = null, Action<ReleaseUpdate, string>? install = null,
        Func<ReleaseUpdate, bool>? confirm = null, string? downloadDirectory = null)
    {
        _updates = updates ?? new ReleaseUpdates(Http);
        _install = install ?? ((update, installer) => App.CurrentApp.InstallUpdate(update, installer));
        _downloadDirectory = downloadDirectory ?? Path.Combine(UserDataLocation.DirectoryPath, "Updates");
        _confirm = confirm ?? (update => MessageBox.Show(Window.GetWindow(this),
            $"WinDesk {update.Version} kurulumu açılsın mı?\n\nDüzenin kaydedilecek ve WinDesk kapanacak. Kurulum sihirbazını tamamlayınca WinDesk'i yeniden başlatabilirsin.\n\nTaşınabilir kullanıyorsan bu işlem WinDesk'i kullanıcı hesabına kurar. Grupların ve kısayolların korunur.",
            "WinDesk güncellemesi", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
        Margin = new Thickness(0, 20, 0, 0);
        Children.Add(new TextBlock { Text = "Güncelleme", FontWeight = FontWeights.SemiBold });
        _status.Foreground = ManagerWindow.Brush("#A4AFC1"); _status.FontSize = 12;
        Children.Add(_status); Children.Add(_progress);
        var buttons = new WrapPanel(); buttons.Children.Add(_action); buttons.Children.Add(_notes); buttons.Children.Add(_cancel); Children.Add(buttons);
        _action.Click += async (_, _) => await RunAsync();
        _cancel.Click += (_, _) => Cancel();
        _notes.Click += (_, _) => {
            try { Process.Start(new ProcessStartInfo(_available!.ReleaseUrl.AbsoluteUri) { UseShellExecute = true }); }
            catch (Win32Exception ex) { App.ShowError("Sürüm notları açılamadı", ex); }
        };
    }

    internal void Cancel() => _operation?.Cancel();

    internal async Task RunAsync()
    {
        if (_operation is not null) return;
        using var operation = new CancellationTokenSource();
        _operation = operation;
        _action.IsEnabled = false; _cancel.Visibility = Visibility.Visible;
        try
        {
            if (_available is null)
            {
                _status.Text = "Yeni sürüm denetleniyor…";
                _available = await _updates.CheckAsync(typeof(App).Assembly.GetName().Version!, operation.Token);
                _status.Text = _available is null ? "Güncelsin. Daha yeni bir kararlı sürüm bulunamadı."
                    : $"WinDesk {_available.Version} hazır ({_available.Size / 1048576.0:0.0} MB). İndirirken çalışmaya devam edebilirsin.";
                _notes.Visibility = _available is null ? Visibility.Collapsed : Visibility.Visible;
                return;
            }
            if (_installer is not null && !File.Exists(_installer)) _installer = null;
            if (_installer is null)
            {
                _status.Text = "Güncelleme indiriliyor…";
                _progress.Visibility = Visibility.Visible; _progress.Value = 0;
                string directory = Path.Combine(_downloadDirectory, Guid.NewGuid().ToString("N"));
                _installer = await _updates.DownloadAsync(_available, directory, new Progress<double>(fraction => {
                    if (operation.IsCancellationRequested) return;
                    _progress.Value = fraction; _status.Text = $"Güncelleme indiriliyor… %{fraction * 100:0}";
                }), operation.Token);
            }
            operation.Token.ThrowIfCancellationRequested();
            _status.Text = "İndirme doğrulandı. Kurulum hazır.";
            if (_confirm(_available)) _install(_available, _installer);
        }
        catch (OperationCanceledException)
        { _status.Text = operation.IsCancellationRequested ? "İşlem iptal edildi." : "Bağlantı zaman aşımına uğradı. Yeniden deneyebilirsin."; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException
            or System.Text.Json.JsonException or InvalidOperationException or System.Collections.Generic.KeyNotFoundException or Win32Exception or FormatException)
        { _installer = null; _status.Text = "Güncelleme tamamlanamadı. Yeniden deneyebilirsin. " + ex.Message; }
        finally
        {
            _operation = null; _action.IsEnabled = true; _cancel.Visibility = Visibility.Collapsed;
            _progress.Visibility = Visibility.Collapsed;
            _action.Content = _installer is not null ? "Kurulumu başlat" : _available is not null ? "Güncellemeyi indir" : "Güncellemeleri denetle";
        }
    }
}
