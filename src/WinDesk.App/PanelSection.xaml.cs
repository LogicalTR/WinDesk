using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using WinDesk.Core;

namespace WinDesk.App;

public partial class PanelSection : UserControl
{
    internal PanelModel Model { get; }
    private readonly DesktopPanel _owner;
    private bool _expanded;
    private readonly List<ContextMenu> _itemMenus = new();
    private App Controller => App.CurrentApp;
    internal double OpenHeight => Model.Items.Count == 0 ? 186 : 62 + Math.Min(3, (int)Math.Ceiling(Model.Items.Count / 4.0)) * 78;
    internal bool IsExpanded => _expanded;
    internal Point HeaderScreenOrigin => Header.PointToScreen(new Point());

    internal PanelSection(PanelModel model, DesktopPanel owner)
    {
        Model = model; _owner = owner;
        InitializeComponent();
        Refresh();
        MouseEnter += (_, _) => _owner.HoverSection(Model.Id);
        DragEnter += (_, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) _owner.Expand(Model.Id); };
        DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None; e.Handled = true; };
        Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) Controller.AddPaths(Model, paths); e.Handled = true; };
        ContextMenu = BuildMenu();
        SizeChanged += (_, _) => _owner.UpdateSize();
    }

    internal void Refresh()
    {
        PanelTitle.Text = Model.Name;
        ItemCount.Text = Model.Items.Count.ToString();
        Accent.Background = ManagerWindow.Brush(Model.Color);
        AreaGrip.ToolTip = Model.StackId.HasValue ? "Birleşik alanı birlikte taşı" : "Paneli taşı";
        System.Windows.Automation.AutomationProperties.SetName(AreaGrip, AreaGrip.ToolTip.ToString());
        Header.ToolTip = Model.StackId.HasValue ? "Sıralamak için başlığı yukarı/aşağı, ayırmak için alanın dışına sürükle" : "Paneli taşımak için sürükle";
        RefreshPin();
        foreach (var oldMenu in _itemMenus) _owner.UntrackMenu(oldMenu);
        _itemMenus.Clear();
        Items.Children.Clear();
        EmptyHint.Visibility = Model.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ItemsScroller.Visibility = Model.Items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        foreach (var shortcut in Model.Items)
        {
            var content = new StackPanel();
            var icon = ShellIcons.Get(ShortcutImporter.IconPath(shortcut));
            if (icon is not null) content.Children.Add(new Image { Source = icon, Width = 30, Height = 30, Margin = new Thickness(0, 2, 0, 6) });
            else content.Children.Add(new TextBlock { Text = "◇", FontSize = 27, HorizontalAlignment = HorizontalAlignment.Center, Height = 38 });
            content.Children.Add(new TextBlock { Text = shortcut.Name, FontSize = 11, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 63 });
            var button = new ShortcutButton(Model, shortcut, _owner) { Content = content, Width = 72, Height = 76, Padding = new Thickness(3), Margin = new Thickness(1), Foreground = Foreground, Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, ToolTip = shortcut.Name };
            button.Style = (Style)FindResource("ShortcutStyle");
            System.Windows.Automation.AutomationProperties.SetName(button, shortcut.Name);
            button.Click += (_, _) => { Controller.ShortcutLauncher(shortcut); _owner.Collapse(); };
            var menu = new ContextMenu();
            var remove = new MenuItem { Header = "Panelden kaldır" };
            remove.Click += (_, _) => Controller.RemoveShortcut(Model, shortcut);
            menu.Items.Add(remove); _owner.TrackMenu(menu); _itemMenus.Add(menu); button.ContextMenu = menu;
            Items.Children.Add(button);
        }
        if (_expanded) SetExpanded(true);
    }

    internal void RefreshPin()
    {
        PinButton.IsChecked = Model.IsPinned;
        PinButton.ToolTip = Model.IsPinned ? "Sabitlemeyi kaldır" : "Grubu açık tut";
        System.Windows.Automation.AutomationProperties.SetName(PinButton, Model.Name + (Model.IsPinned ? " sabitlemesini kaldır" : " grubunu açık tut"));
    }

    internal void SetExpanded(bool value, bool animate = true)
    {
        _expanded = value;
        if (value) Body.Visibility = Visibility.Visible;
        double start = Height, target = value ? OpenHeight : 44;
        BeginAnimation(HeightProperty, null); Height = target;
        if (!animate) { Body.Visibility = value ? Visibility.Visible : Visibility.Collapsed; _owner.UpdateSize(); return; }
        var animation = new DoubleAnimation(start, target, TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 160 : 0))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
        animation.Completed += (_, _) => { if (!_expanded) Body.Visibility = Visibility.Collapsed; _owner.UpdateSize(); };
        BeginAnimation(HeightProperty, animation);
        _owner.UpdateSize();
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        // A group title detaches that section; the separate grip moves the entire area.
        if (e.ChangedButton != MouseButton.Left) return;
        _owner.BeginSectionDrag(Model.Id, PointerScreen(Header)); Header.CaptureMouse(); e.Handled = true;
    }
    private static DesktopNative.Point PointerScreen(UIElement element)
    { var point = element.PointToScreen(Mouse.GetPosition(element)); return new DesktopNative.Point((int)Math.Round(point.X), (int)Math.Round(point.Y)); }
    private void Grip_Started(object sender, DragStartedEventArgs e) { _owner.BeginDrag(PointerScreen(AreaGrip)); e.Handled = true; }
    private void Grip_Delta(object sender, DragDeltaEventArgs e) { _owner.ContinueDrag(PointerScreen(AreaGrip)); e.Handled = true; }
    private void Grip_Completed(object sender, DragCompletedEventArgs e) { _owner.EndDrag(PointerScreen(AreaGrip), commit: !e.Canceled); e.Handled = true; }
    private void Header_MouseMove(object sender, MouseEventArgs e) { if (Header.IsMouseCaptured) _owner.ContinueDrag(PointerScreen(Header)); }
    private void Header_MouseUp(object sender, MouseButtonEventArgs e)
    { if (e.ChangedButton == MouseButton.Left && Header.IsMouseCaptured) { _owner.EndDrag(PointerScreen(Header)); Header.ReleaseMouseCapture(); e.Handled = true; } }
    private void Header_LostCapture(object sender, MouseEventArgs e)
    { if (ReferenceEquals(e.OriginalSource, Header)) _owner.EndDrag(commit: false); }

    internal bool ContainsScreenPoint(DesktopNative.Point point)
    {
        if (!IsVisible || !_owner.IsDropAvailable || !DesktopNative.GetWindowRect(_owner.Handle, out var bounds) ||
            point.X < bounds.Left || point.X >= bounds.Right || point.Y < bounds.Top || point.Y >= bounds.Bottom) return false;
        var local = PointFromScreen(new Point(point.X, point.Y));
        return local.X >= 0 && local.X < ActualWidth && local.Y >= 0 && local.Y < ActualHeight;
    }

    internal int InsertionIndex(DesktopNative.Point point)
    {
        if (Items.Children.Count == 0 || !Items.IsVisible || PresentationSource.FromVisual(Items) is null) return Model.Items.Count;
        var local = PointFromScreen(new Point(point.X, point.Y));
        if (local.Y < 44) return Model.Items.Count;
        var position = Items.PointFromScreen(new Point(point.X, point.Y));
        for (int i = 0; i < Items.Children.Count; i++)
        {
            var item = (FrameworkElement)Items.Children[i];
            var origin = item.TranslatePoint(new Point(), Items);
            if (position.Y < origin.Y) return i;
            if (position.Y < origin.Y + item.ActualHeight && position.X < origin.X + item.ActualWidth / 2) return i;
            bool endOfRow = i == Items.Children.Count - 1 ||
                ((FrameworkElement)Items.Children[i + 1]).TranslatePoint(new Point(), Items).Y > origin.Y + 1;
            if (endOfRow && position.Y < origin.Y + item.ActualHeight) return i + 1;
        }
        return Model.Items.Count;
    }

    internal void ShowShortcutDrop(int? index)
    {
        Body.Background = index.HasValue ? ManagerWindow.Brush("#273C40") : Brushes.Transparent;
        DropMarker.Visibility = Visibility.Collapsed;
        if (index is not int slot || Items.Children.Count == 0 || !Items.IsVisible || PresentationSource.FromVisual(Items) is null) return;
        var anchor = (FrameworkElement)Items.Children[Math.Min(slot, Items.Children.Count - 1)];
        var origin = anchor.TranslatePoint(new Point(), DropMarkerLayer);
        Canvas.SetLeft(DropMarker, origin.X + (slot == Items.Children.Count ? anchor.ActualWidth : 0));
        Canvas.SetTop(DropMarker, origin.Y + 3);
        DropMarker.Visibility = Visibility.Visible;
    }

    internal void ExpandForDrop() => _owner.Expand(Model.Id);

    internal void ScrollForDrop(DesktopNative.Point point)
    {
        if (!ItemsScroller.IsVisible || ItemsScroller.ScrollableHeight <= 0) return;
        var local = ItemsScroller.PointFromScreen(new Point(point.X, point.Y));
        if (local.Y >= 0 && local.Y < 20) ItemsScroller.ScrollToVerticalOffset(ItemsScroller.VerticalOffset - 12);
        else if (local.Y <= ItemsScroller.ActualHeight && local.Y > ItemsScroller.ActualHeight - 20)
            ItemsScroller.ScrollToVerticalOffset(ItemsScroller.VerticalOffset + 12);
    }
    private void Add_Click(object sender, RoutedEventArgs e) => _owner.WithDialog(() => Controller.AddFiles(Model));
    private void Pin_Click(object sender, RoutedEventArgs e)
    { Controller.SetPanelPinned(Model, PinButton.IsChecked == true); e.Handled = true; }
    private void Menu_Click(object sender, RoutedEventArgs e)
    { ContextMenu.PlacementTarget = (Button)sender; ContextMenu.IsOpen = true; e.Handled = true; }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        AddMenuItem(menu, "Kısayol ekle…", () => _owner.WithDialog(() => Controller.AddFiles(Model)));
        AddMenuItem(menu, "Klasör ekle…", () => _owner.WithDialog(() => Controller.AddFolder(Model)));
        AddMenuItem(menu, "Adı ve rengi düzenle…", () => _owner.WithDialog(() => Controller.EditPanel(Model)));
        if (Model.StackId is Guid stackId) AddMenuItem(menu, "Birleşik alanı ayır", () => Controller.SeparateStack(stackId));
        menu.Items.Add(new Separator());
        AddMenuItem(menu, "Ayarlar", Controller.OpenManager);
        AddMenuItem(menu, "Grubu kaldır…", () => _owner.WithDialog(() => Controller.RemovePanel(Model)));
        _owner.TrackMenu(menu); return menu;
    }
    private static void AddMenuItem(ContextMenu menu, string name, Action action)
    { var item = new MenuItem { Header = name }; item.Click += (_, _) => action(); menu.Items.Add(item); }
}
