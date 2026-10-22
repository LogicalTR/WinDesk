using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WinDesk.App;

internal sealed class SettingsWindow : Window
{
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly Button _visibility;
    private readonly TextBlock _status;
    private App Controller => App.CurrentApp;

    internal SettingsWindow()
    {
        Title = "WinDesk · Ayarlar"; Width = 450; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = ManagerWindow.Brush("#171F2C");
        Foreground = ManagerWindow.Brush("#EDF1F7");
        var body = new StackPanel { Margin = new Thickness(26) };
        body.Children.Add(new TextBlock { Text = "Ayarlar", FontSize = 23, FontWeight = FontWeights.SemiBold });
        body.Children.Add(new TextBlock { Text = "Gruplarını masaüstünden yönet.", Foreground = ManagerWindow.Brush("#A4AFC1"), Margin = new Thickness(0, 6, 0, 24) });
        var heading = new DockPanel();
        var duration = new TextBlock { Foreground = ManagerWindow.Brush("#73D9C3"), HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(duration, Dock.Right); heading.Children.Add(duration);
        heading.Children.Add(new TextBlock { Text = "Otomatik kapanma", FontWeight = FontWeights.SemiBold }); body.Children.Add(heading);
        var slider = new Slider { Minimum = 200, Maximum = 2000, TickFrequency = 50, IsSnapToTickEnabled = true,
            Value = Controller.Workspace.CloseDelayMs, Margin = new Thickness(0, 14, 0, 10) };
        System.Windows.Automation.AutomationProperties.SetName(slider, "Panel kapanma gecikmesi");
        duration.Text = $"{slider.Value / 1000:0.00} saniye";
        slider.ValueChanged += (_, e) => {
            duration.Text = $"{e.NewValue / 1000:0.00} saniye";
            Controller.SetCloseDelay((int)e.NewValue);
            _saveTimer.Stop(); _saveTimer.Start();
        };
        body.Children.Add(slider);
        body.Children.Add(new TextBlock { Text = "Fare uzaklaşınca sabitlenmemiş gruplar kapanır. Raptiyeyle sabitlediklerin her zaman açık kalır.",
            TextWrapping = TextWrapping.Wrap, Foreground = ManagerWindow.Brush("#A4AFC1"), LineHeight = 19 });
        _visibility = new Button { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 24, 0, 18) };
        _visibility.Click += (_, _) => Controller.TogglePanels(); body.Children.Add(_visibility);
        _status = new TextBlock { FontSize = 11, Foreground = ManagerWindow.Brush("#A4AFC1"), TextWrapping = TextWrapping.Wrap }; body.Children.Add(_status);
        body.Children.Add(new TextBlock { Text = $"WinDesk {typeof(App).Assembly.GetName().Version?.ToString(3)}", FontSize = 11, Foreground = ManagerWindow.Brush("#75859B"), Margin = new Thickness(0, 8, 0, 0) });
        Content = body;
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Controller.Save(); };
        Controller.WorkspaceChanged += Refresh; Controller.DesktopStatusChanged += Refresh;
        Closing += OnClosing; Refresh();
    }

    private void Refresh()
    { _visibility.Content = Controller.PanelsHidden ? "Panelleri göster" : "Panelleri gizle"; _status.Text = Controller.DesktopStatus; }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _saveTimer.Stop();
        if (Controller.IsExiting) return;
        Controller.Save(); e.Cancel = true; Hide();
    }
}
