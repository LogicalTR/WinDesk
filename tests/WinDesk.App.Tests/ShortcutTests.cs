using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WinDesk.App;
using WinDesk.Core;

internal static class ShortcutTests
{
    internal static async Task Run(App app, string directory, Action<bool, string> check)
    {
        app.Workspace.Panels.Clear(); app.Workspace.Stacks.Clear();
        var source = new PanelModel { Name = "Kaynak", X = 90, Y = 130, IsPinned = true };
        var target = new PanelModel { Name = "Hedef", X = 650, Y = 200, IsPinned = true };
        var a = new ShortcutModel { Name = "A", Path = Path.Combine(directory, "A.txt") };
        var b = new ShortcutModel { Name = "B", Path = Path.Combine(directory, "B.txt") };
        var c = new ShortcutModel { Name = "C", Path = Path.Combine(directory, "C.txt") };
        foreach (var item in new[] { a, b, c }) File.WriteAllText(item.Path, "Original " + item.Name);
        source.Items.AddRange(new[] { a, b, c });
        app.Workspace.Panels.AddRange(new[] { source, target }); app.RebuildPanels();
        await Task.Delay(200);
        var sourcePanel = app.DesktopPanels.Single(p => p.Id == source.Id);
        var targetPanel = app.DesktopPanels.Single(p => p.Id == target.Id);
        var parent = sourcePanel.DesktopParent;
        app.WindowAtPoint = _ => parent;
        app.DesktopShortcutDirectory = Path.Combine(directory, "export-desktop");
        try
        {
            var section = sourcePanel.Sections.Single();
            var grip = (Thumb)section.FindName("AreaGrip");
            check(grip.IsVisible && grip.ActualWidth >= 30, "Standalone panels expose a full-width Thumb grip with mouse capture support");
            var items = (WrapPanel)section.FindName("Items");
            DesktopNative.Point At(FrameworkElement item, double x, double y)
            { var point = item.PointToScreen(new Point(x, y)); return new DesktopNative.Point((int)point.X, (int)point.Y); }
            var beforeFirst = At((FrameworkElement)items.Children[0], 3, 35);
            var afterLast = At((FrameworkElement)items.Children[2], 68, 35);
            check(section.InsertionIndex(beforeFirst) == 0 && section.InsertionIndex(afterLast) == 3,
                "Grid hit testing distinguishes insertion before the first and after the last icon");
            app.BeginShortcutDrag(sourcePanel, source, a); app.UpdateShortcutDrag(afterLast); app.EndShortcutDrag(afterLast);
            await Task.Delay(160);
            check(source.Items.SequenceEqual(new[] { b, c, a }), "A shortcut drag reorders the group through the desktop gesture path");
            check(File.ReadAllText(a.Path) == "Original A", "Reordering never launches or modifies the original file");
            var destination = At(targetPanel.Sections.Single(), 120, 110);
            app.BeginShortcutDrag(sourcePanel, source, b); app.UpdateShortcutDrag(destination); app.EndShortcutDrag(destination);
            await Task.Delay(160);
            check(target.Items.Single() == b && source.Items.SequenceEqual(new[] { c, a }), "Dropping in an empty group moves one shortcut and refreshes both groups");
            app.BeginShortcutDrag(sourcePanel, source, c); app.UpdateShortcutDrag(destination); app.EndShortcutDrag(destination, false);
            await Task.Delay(100);
            check(source.Items.Contains(c) && target.Items.Count == 1 && !app.IsShortcutDragging, "Interrupted item drags leave both groups unchanged and dispose their preview");
            app.BeginShortcutDrag(sourcePanel, source, c);
            app.WindowAtPoint = _ => IntPtr.Zero;
            app.EndShortcutDrag(destination); await Task.Delay(100);
            check(source.Items.Contains(c) && target.Items.Count == 1, "Item drops over another application cannot move or export a shortcut");
            app.WindowAtPoint = _ => parent;
            var outside = new DesktopNative.Point(1250, 700);
            app.BeginShortcutDrag(sourcePanel, source, c); app.UpdateShortcutDrag(outside); app.EndShortcutDrag(outside);
            await Task.Delay(220);
            string exported = Path.Combine(app.DesktopShortcutDirectory, "C.lnk");
            check(File.Exists(exported) && new FileInfo(exported).Length > 0 && !source.Items.Contains(c) && File.ReadAllText(c.Path) == "Original C",
                "Dropping outside exports a real Windows shortcut before removing only its group entry");
            var exportedItem = new ShortcutModel { Name = "C", Path = exported };
            check(DesktopShortcutExporter.Export(exportedItem, app.DesktopShortcutDirectory) == exported && Directory.GetFiles(app.DesktopShortcutDirectory).Length == 1,
                "An existing desktop shortcut is reused without a duplicate");
            string duplicate = DesktopShortcutExporter.Export(c, app.DesktopShortcutDirectory);
            check(duplicate != exported && File.Exists(exported) && File.Exists(duplicate), "Desktop filename collisions preserve the existing shortcut");
            var bad = new ShortcutModel { Name = "Missing", Path = Path.Combine(directory, "does-not-exist.exe") };
            bool failed = false;
            try { DesktopShortcutExporter.Export(bad, app.DesktopShortcutDirectory); } catch (FileNotFoundException) { failed = true; }
            check(failed && Directory.GetFiles(app.DesktopShortcutDirectory).Length == 2, "Missing shortcut targets cannot create a broken export");
            var url = Path.Combine(directory, "Website.url"); File.WriteAllText(url, "[InternetShortcut]\r\nURL=https://example.com\r\n");
            string exportedUrl = DesktopShortcutExporter.Export(new ShortcutModel { Name = "Website", Path = url }, app.DesktopShortcutDirectory);
            check(File.ReadAllText(exportedUrl) == File.ReadAllText(url), "Internet shortcuts preserve their URL data when exported");
            var loaded = new WorkspaceStore(Path.Combine(directory, "workspace.json")).Load();
            check(loaded.Panels[0].Items.Single().Name == "A" && loaded.Panels[1].Items.Single().Name == "B", "Item transfers and removals survive a saved layout reload");

            // Regression from the screenshot: the left grip stays outside the
            // target while a substantial part of the dragged area overlaps it.
            sourcePanel.MoveTo(90, 130); targetPanel.MoveTo(650, 200);
            var start = new DesktopNative.Point(106, 150);
            var overlap = new DesktopNative.Point(570, 200);
            sourcePanel.BeginDrag(start); sourcePanel.ContinueDrag(overlap);
            check(overlap.X < 650 && app.FindDropTarget(sourcePanel, overlap) == targetPanel,
                "Substantial panel overlap selects a merge target even when the left grip is outside it");
            app.WindowAtPoint = p => p.X < 650 ? parent : IntPtr.Zero;
            check(app.FindDropTarget(sourcePanel, overlap) is null, "Overlap merging rejects a destination covered by another window");
            app.WindowAtPoint = _ => parent;
            sourcePanel.EndDrag(overlap, false); sourcePanel.MoveTo(90, 130);
            var graze = new DesktopNative.Point(340, 200);
            sourcePanel.BeginDrag(start); sourcePanel.ContinueDrag(graze);
            check(app.FindDropTarget(sourcePanel, graze) is null, "A tiny overlap along the edge cannot accidentally merge areas");
            sourcePanel.EndDrag(graze, false); sourcePanel.MoveTo(90, 130);
            sourcePanel.BeginDrag(start); sourcePanel.ContinueDrag(overlap); sourcePanel.EndDrag(overlap);
            await Task.Delay(200);
            check(app.Workspace.Stacks.Count == 1 && source.StackId == target.StackId && app.DesktopPanels.Count() == 1,
                "Releasing an overlapping area commits the merge while preserving shortcut membership");
            var merged = app.DesktopPanels.Single();
            app.SetPanelPinned(source, false); merged.Collapse(); await Task.Delay(200);
            var from = merged.Sections.Single(s => s.Model.Id == target.Id);
            var to = merged.Sections.Single(s => s.Model.Id == source.Id);
            app.SetPanelPinned(target, false); merged.Expand(target.Id); await Task.Delay(200);
            var toHeader = At(to, 90, 20);
            app.BeginShortcutDrag(merged, target, b); app.UpdateShortcutDrag(toHeader); await Task.Delay(250);
            check(to.IsExpanded, "Hovering a collapsed accordion member while carrying a shortcut opens its body");
            check(from.IsExpanded && !target.IsPinned, "The unpinned source stays open during a drag so opening its sibling cannot lose mouse capture");
            var intoMember = At(to, 120, 90);
            app.EndShortcutDrag(intoMember); await Task.Delay(180);
            check(source.Items.Contains(b) && target.Items.Count == 0 && source.StackId == target.StackId,
                "Shortcut transfer between accordion members leaves the group structure intact");
        }
        finally { app.EndShortcutDrag(default, false); app.WindowAtPoint = DesktopNative.WindowFromPoint; }
    }
}
