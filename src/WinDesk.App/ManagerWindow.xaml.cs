using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WinDesk.Core;

namespace WinDesk.App;

public partial class ManagerWindow : Window
{
    private bool _ready;
    private readonly HashSet<Guid> _selected = new();
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private App Controller => App.CurrentApp;

    public ManagerWindow()
    {
        InitializeComponent();
        CloseDelay.Value = Controller.Workspace.CloseDelayMs;
        _ready = true;
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Controller.Save(); };
        Controller.WorkspaceChanged += Refresh;
        Controller.DesktopStatusChanged += RefreshDesktopStatus;
        Refresh();
        Closing += OnClosing;
    }

    private void Refresh()
    {
        PanelCards.Children.Clear();
        _selected.RemoveWhere(id => !Controller.Workspace.Panels.Any(p => p.Id == id && p.StackId is null));
        Summary.Text = $"{Controller.Workspace.Panels.Count} grup · {Controller.Workspace.Stacks.Count} birleşik alan · {Controller.Workspace.Panels.Sum(p => p.Items.Count)} kısayol";
        VisibilityButton.Content = Controller.PanelsHidden ? "Panelleri göster" : "Panelleri gizle";
        foreach (var stack in Controller.Workspace.Stacks) PanelCards.Children.Add(BuildStackCard(stack));
        foreach (var panel in Controller.Workspace.Panels.Where(p => p.StackId is null)) PanelCards.Children.Add(BuildCard(panel));
        UpdateSelection(); RefreshDesktopStatus();
        if (Controller.Workspace.Panels.Count == 0)
            PanelCards.Children.Add(new TextBlock { Text = "İlk panelini oluştur; uygulama kısayollarını içine bırak.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 20, 0, 20) });
    }

    private void RefreshDesktopStatus() => DesktopStatusText.Text = Controller.DesktopStatus;
    private void UpdateSelection()
    {
        MergeButton.IsEnabled = _selected.Count >= 2;
        SelectionHint.Text = _selected.Count == 0 ? "Birleştirmek için en az iki grup seçin." : $"{_selected.Count} grup seçili · Tek alan, akordiyon görünümü";
    }
    private void Merge_Click(object sender, RoutedEventArgs e) => Controller.CombinePanels(_selected.ToArray());

    private Border BuildStackCard(PanelStack stack)
    {
        var content = new StackPanel();
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var actions = new WrapPanel();
        AddAction(actions, "Adını değiştir", () => Controller.RenameStack(stack));
        AddAction(actions, "Grupları ayır", () => Controller.SeparateStack(stack.Id));
        DockPanel.SetDock(actions, Dock.Right); heading.Children.Add(actions);
        heading.Children.Add(new TextBlock { Text = stack.Name, FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        content.Children.Add(heading);
        content.Children.Add(new TextBlock { Text = "Başlıktan ayır · Tutamaçtan birlikte taşı · Sabitlenenler açık kalır", FontSize = 12, Foreground = Brush("#A4AFC1"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        foreach (var panel in Controller.Workspace.Panels.Where(p => p.StackId == stack.Id)) content.Children.Add(BuildCard(panel));
        return new Border { Child = content, Background = Brush("#15282A"), BorderBrush = Brush("#47675F"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 12) };
    }

    private Border BuildCard(PanelModel panel)
    {
        var content = new StackPanel();
        var heading = new DockPanel();
        var show = new Button { Content = "Masaüstünde aç", Padding = new Thickness(10, 6, 10, 6), ToolTip = "Grubu görmek için masaüstüne dönün." };
        show.Click += (_, _) => Controller.RevealPanel(panel);
        DockPanel.SetDock(show, Dock.Right); heading.Children.Add(show);
        if (panel.StackId is null)
        {
            var select = new CheckBox { IsChecked = _selected.Contains(panel.Id), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0), ToolTip = "Birleştirmek için seç" };
            System.Windows.Automation.AutomationProperties.SetName(select, panel.Name + " grubunu seç");
            select.Checked += (_, _) => { _selected.Add(panel.Id); UpdateSelection(); };
            select.Unchecked += (_, _) => { _selected.Remove(panel.Id); UpdateSelection(); };
            DockPanel.SetDock(select, Dock.Left); heading.Children.Add(select);
        }
        var marker = new Border { Background = Brush(panel.Color), Width = 4, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 12, 0) };
        DockPanel.SetDock(marker, Dock.Left); heading.Children.Add(marker);
        var title = new StackPanel();
        title.Children.Add(new TextBlock { Text = panel.Name, FontSize = 16, FontWeight = FontWeights.SemiBold });
        title.Children.Add(new TextBlock { Text = panel.Items.Count == 0 ? "Kısayollarını bu karta veya masaüstü paneline bırak." : $"{panel.Items.Count} öğe · Fareyle açılır", Foreground = Brush("#A4AFC1"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 14, 0) });
        heading.Children.Add(title); content.Children.Add(heading);
        var actions = new WrapPanel { Margin = new Thickness(16, 16, 0, 0) };
        AddAction(actions, "+ Kısayol", () => Controller.AddFiles(panel));
        AddAction(actions, "+ Klasör", () => Controller.AddFolder(panel));
        AddAction(actions, "Düzenle", () => Controller.EditPanel(panel));
        AddAction(actions, "Kaldır", () => Controller.RemovePanel(panel));
        content.Children.Add(actions);
        var border = new Border { Background = Brush("#1B2230"), BorderBrush = Brush("#303A4B"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(18), Margin = new Thickness(0, 0, 0, 12), Child = content, AllowDrop = true };
        border.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None; e.Handled = true; };
        border.Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) Controller.AddPaths(panel, paths); e.Handled = true; };
        return border;
    }

    private static void AddAction(Panel parent, string text, Action action)
    {
        var button = new Button { Content = text, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 8, 0), FontSize = 12, Background = Brushes.Transparent };
        button.Click += (_, _) => action(); parent.Children.Add(button);
    }

    internal static Brush Brush(string value) => (Brush)new BrushConverter().ConvertFromString(value)!;
    private void NewPanel_Click(object sender, RoutedEventArgs e) => Controller.CreatePanel();
    private void Visibility_Click(object sender, RoutedEventArgs e) => Controller.TogglePanels();
    private void CloseDelay_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DelayLabel is not null) DelayLabel.Text = $"{e.NewValue / 1000:0.00} saniye";
        if (!_ready) return;
        Controller.SetCloseDelay((int)e.NewValue);
        _saveTimer.Stop(); _saveTimer.Start();
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (Controller.IsExiting) return;
        e.Cancel = true; Hide(); Controller.Save();
    }
}
