using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using WinDesk.App;
using WinDesk.Core;

internal static class DisplayBehaviorTests
{
    internal static async Task Run(App app, Action<bool, string> check)
    {
        app.Workspace.Panels.Clear(); app.Workspace.Stacks.Clear();
        var model = new PanelModel { Name = "Kaydırma", IsPinned = true };
        for (int i = 1; i <= 20; i++) model.Items.Add(new ShortcutModel { Name = $"Kısayol {i:00}", Path = Environment.ProcessPath! });
        app.Workspace.Panels.Add(model); app.RebuildPanels();
        var panel = app.DesktopPanels.Single(); var section = panel.Sections.Single();
        panel.UpdateLayout(); await Task.Delay(200);
        check(Math.Abs(section.OpenHeight - 296) < 1 && Math.Abs(section.ItemsScroller.ViewportHeight - 234) < 1,
            "Twenty shortcuts keep a three-row viewport and a fixed panel height");
        var bar = (ScrollBar)section.ItemsScroller.Template.FindName("PART_VerticalScrollBar", section.ItemsScroller);
        check(bar.IsVisible && section.ItemsScroller.ScrollableHeight > 150,
            "More than twelve shortcuts expose a usable vertical scrollbar");
        section.ItemsScroller.ScrollToEnd(); panel.UpdateLayout();
        check(section.ItemsScroller.VerticalOffset > 150 && section.Items.Children[19].TranslatePoint(new Point(), section.ItemsScroller).Y < 234,
            "Scrolling reaches the final shortcut without growing the panel");
        model.Items.RemoveRange(12, 8); panel.Refresh(); panel.UpdateLayout();
        await Task.Delay(250); panel.UpdateLayout();
        Console.WriteLine($"After removal: bar={section.ItemsScroller.ComputedVerticalScrollBarVisibility}, offset={section.ItemsScroller.VerticalOffset}, viewport={section.ItemsScroller.ViewportWidth}x{section.ItemsScroller.ViewportHeight}, extent={section.ItemsScroller.ExtentWidth}x{section.ItemsScroller.ExtentHeight}, panel={panel.Width}x{panel.Height}, section={section.ActualWidth}x{section.ActualHeight}");
        check(section.ItemsScroller.ComputedVerticalScrollBarVisibility == Visibility.Collapsed && section.ItemsScroller.VerticalOffset == 0,
            "Three rows fit without a scrollbar and clamp the old scroll offset");
        panel.Hide(); model.IsPinned = false; panel.Refresh();
        app.SetCloseDelay(200); panel.Expand(); app.SetCloseDelay(1500);
        await Task.Delay(700);
        check(section.IsExpanded, "Changing the delay updates a countdown already in progress");
        await Task.Delay(1050);
        check(!section.IsExpanded, "An unpinned group closes after the selected 1.5 second delay");
        model.IsPinned = true; panel.Refresh(); app.SetCloseDelay(200); panel.Expand();
        await Task.Delay(500);
        check(section.IsExpanded, "A pinned group remains open regardless of the automatic close delay");
    }
}
