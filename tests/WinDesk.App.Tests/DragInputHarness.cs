using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using WinDesk.App;
using WinDesk.Core;

// Actual mouse/capture/routed-event exercise in a targetable test window.
// The same PanelSection controls point at real, isolated desktop models.
// Only this window's occlusion result is mapped to the desktop for the test.
internal static class DragInputHarness
{
    internal static void Open(App app, string directory, bool scrollTest = false)
    {
        Directory.CreateDirectory(directory);
        var canvas = new Canvas { Background = new SolidColorBrush(Color.FromRgb(36, 45, 58)) };
        var status = new TextBlock { Foreground = Brushes.LightGreen, FontSize = 16, TextWrapping = TextWrapping.Wrap, Width = 980 };
        var switchTest = new Button { Content = "Kısayol testini hazırla", Width = 190, Height = 34 };
        var sortTest = new Button { Content = "Başlık sıralama testi", Width = 190, Height = 34 };
        var window = new Window { Title = "WinDesk · Gerçek fare testi", Width = 1100, Height = 650, WindowStartupLocation = WindowStartupLocation.CenterScreen, Content = canvas };
        string log = Path.Combine(directory, "mouse-results.txt");
        string fingerprint = "";
        bool itemMode = false;
        bool sortMode = false;
        int launched = 0;
        void Record(string message) { status.Text = message; File.AppendAllText(log, message + Environment.NewLine); }
        void Render()
        {
            canvas.Children.Clear();
            Canvas.SetLeft(status, 32); Canvas.SetTop(status, 415); canvas.Children.Add(status);
            Canvas.SetLeft(switchTest, 32); Canvas.SetTop(switchTest, 550); canvas.Children.Add(switchTest);
            Canvas.SetLeft(sortTest, 245); Canvas.SetTop(sortTest, 550); canvas.Children.Add(sortTest);
            int n = 0;
            foreach (var owner in app.DesktopPanels)
            {
                double x = n++ == 0 ? 40 : 520, y = n == 1 ? 55 : 115;
                var cloneStack = new StackPanel();
                foreach (var model in app.Workspace.Panels.Where(p => owner.Contains(p.Id)))
                {
                    var section = new PanelSection(model, owner);
                    section.SetExpanded(model.IsPinned, false);
                    if (scrollTest) section.ItemsScroller.ScrollChanged += (_, e) => {
                        if (Math.Abs(e.VerticalChange) > 0.1) Record($"Kaydırma: {e.VerticalOffset:0}; görünür alan: {e.ViewportHeight:0}; içerik: {e.ExtentHeight:0}");
                    };
                    cloneStack.Children.Add(section);
                }
                var frame = new Border { Width = 336, Child = cloneStack, Background = new SolidColorBrush(Color.FromRgb(26, 34, 48)), BorderThickness = new Thickness(1), BorderBrush = Brushes.SlateGray, CornerRadius = new CornerRadius(10) };
                Canvas.SetLeft(frame, x); Canvas.SetTop(frame, y); canvas.Children.Add(frame);
                canvas.UpdateLayout();
                var screen = canvas.PointToScreen(new Point(x, y));
                double scale = VisualTreeHelper.GetDpi(canvas).DpiScaleX;
                owner.MoveTo(screen.X / scale, screen.Y / scale);
            }
        }
        void Prepare(bool items)
        {
            itemMode = items;
            sortMode = false;
            app.Workspace.Panels.Clear(); app.Workspace.Stacks.Clear();
            var left = new PanelModel { Name = "Kaynak", IsPinned = true };
            var right = new PanelModel { Name = "Hedef", IsPinned = true, Color = "#AFA4F4" };
            if (items)
                foreach (string name in scrollTest ? Enumerable.Range(1, 20).Select(n => $"Kısayol {n:00}") : new[] { "Alfa", "Beta", "Gama" })
                {
                    string path = Path.Combine(directory, name + ".txt"); File.WriteAllText(path, "Only test data.");
                    left.Items.Add(new ShortcutModel { Name = name, Path = path });
                }
            app.Workspace.Panels.AddRange(new[] { left, right }); app.RebuildPanels();
            app.DesktopShortcutDirectory = Path.Combine(directory, "mouse-desktop");
            fingerprint = ""; Render();
            Record(items ? "Kısayollar: Alfa, Beta, Gama → sıra değiştir, gruplar arasında taşı, dışarı bırak." : "Sol noktalı tutamacı sürükle: imleç Hedef'in solunda kalırken paneller örtüşsün.");
        }
        switchTest.Click += (_, _) => Prepare(true);
        sortTest.Click += (_, _) =>
        {
            sortMode = true; itemMode = false;
            app.Workspace.Panels.Clear(); app.Workspace.Stacks.Clear();
            var a = new PanelModel { Name = "Tarayıcılar" };
            var b = new PanelModel { Name = "İş ve üretim", IsPinned = true, Color = "#AFA4F4" };
            var c = new PanelModel { Name = "Günlük", Color = "#EFC17A" };
            app.Workspace.Panels.AddRange(new[] { a, b, c }); app.Workspace.Combine(new[] { a.Id, b.Id, c.Id }, "Başlık sırası");
            app.RebuildPanels(); fingerprint = ""; Render();
            Record("Başlık metninden sürükleyin: Tarayıcılar → Günlük başlığının altı; sonra tekrar en üst.");
        };
        app.WorkspaceChanged += () =>
        {
            string next = string.Join("|", app.Workspace.Panels.Select(p => $"{p.Id}:{p.StackId}:{string.Join(',', p.Items.Select(i => i.Name))}"));
            if (next == fingerprint) return;
            fingerprint = next;
            if (!itemMode && !sortMode && app.Workspace.Stacks.Count == 1) Record("PASS: gerçek fareyle noktalı tutamaçtan sürükleme ve örtüşerek birleşme.");
            if (sortMode) Record("Başlık sırası: " + string.Join(" → ", app.Workspace.Panels.Select(p => p.Name)));
            if (itemMode) Record("Gruplar: " + string.Join(" / ", app.Workspace.Panels.Select(p => p.Name + "=[" + string.Join(",", p.Items.Select(i => i.Name)) + "]")) + $"; yanlış açılış={launched}");
            app.Dispatcher.BeginInvoke(Render);
        };
        window.Closed += (_, _) => { app.WindowAtPoint = DesktopNative.WindowFromPoint; app.ExitApp(); };
        window.Show();
        var ownHandle = new WindowInteropHelper(window).Handle;
        app.WindowAtPoint = point =>
        {
            var hit = DesktopNative.WindowFromPoint(point);
            return DesktopNative.GetAncestor(hit, 2) == ownHandle ? app.DesktopPanels.First().DesktopParent : hit;
        };
        // Launch callbacks are replaced only in this isolated fixture, so a bad
        // mouse-up would be counted rather than open a user application.
        app.ShortcutLauncher = _ => { launched++; Record("FAIL: sürükleme kısayolu açtı."); };
        Prepare(scrollTest);
    }
}
