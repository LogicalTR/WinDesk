using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinDesk.App;
using WinDesk.Core;

internal static class SectionOrderTests
{
    internal static async Task Run(App app, string directory, Action<bool, string> check)
    {
        app.Workspace.Panels.Clear(); app.Workspace.Stacks.Clear();
        var first = new PanelModel { Name = "Tarayıcılar", X = 600, Y = 100 };
        var middle = new PanelModel { Name = "İş ve üretim", IsPinned = true };
        var last = new PanelModel { Name = "Günlük" };
        first.AddPaths(new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe") });
        app.Workspace.Panels.AddRange(new[] { first, middle, last });
        var stack = app.Workspace.Combine(new[] { first.Id, middle.Id, last.Id }, "Başlık sıralama");
        app.RebuildPanels(); await Task.Delay(150);
        var area = app.DesktopPanels.Single(); var handle = area.Handle; var parent = area.DesktopParent;
        app.WindowAtPoint = _ => parent;
        DesktopNative.Point OnHeader(Guid id, double y)
        {
            var point = area.Sections.Single(s => s.Model.Id == id).HeaderScreenOrigin;
            return new DesktopNative.Point((int)point.X + 95, (int)point.Y + (int)y);
        }
        Guid[] Order() => app.Workspace.Panels.Where(p => p.StackId == stack.Id).Select(p => p.Id).ToArray();
        try
        {
            var original = Order();
            var grab = OnHeader(first.Id, 20); var end = OnHeader(last.Id, 35);
            area.BeginSectionDrag(first.Id, grab); area.ContinueDrag(end);
            check(area.SectionDropIndex == 3, "Dragging a title inside the accordion shows the last insertion gap instead of the detach hint");
            area.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)area.Width, (int)area.Height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(area);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(directory, "section-order-hint.png"))) encoder.Save(file);
            area.EndDrag(end, false); await Task.Delay(100);
            check(Order().SequenceEqual(original) && area.SectionDropIndex is null, "Losing mouse capture clears the insertion line without changing group order");
            area.BeginSectionDrag(first.Id, grab); area.ContinueDrag(end); area.EndDrag(end); await Task.Delay(180);
            check(Order().SequenceEqual(new[] { middle.Id, last.Id, first.Id }) && area.Sections.Select(s => s.Model.Id).SequenceEqual(Order()),
                "Releasing a title below the last header updates both saved and visible group order");
            check(area.Handle == handle && stack.X == 600 && stack.Y == 100 && middle.IsPinned && area.IsSectionExpanded(middle.Id) && first.Items.Count == 1,
                "Sorting keeps the same desktop window, position, pinned body and shortcut contents");
            grab = OnHeader(first.Id, 20); var top = OnHeader(middle.Id, 5);
            area.BeginSectionDrag(first.Id, grab); area.ContinueDrag(top); area.EndDrag(top); await Task.Delay(180);
            check(Order().SequenceEqual(original), "Dragging the last header upward moves it to the first position");
            grab = OnHeader(first.Id, 12); var self = OnHeader(first.Id, 32);
            area.BeginSectionDrag(first.Id, grab); area.ContinueDrag(self); area.EndDrag(self); await Task.Delay(100);
            check(Order().SequenceEqual(original) && area.Handle == handle, "A drop within the same header preserves its position and native window");
            grab = OnHeader(first.Id, 20); end = OnHeader(last.Id, 35);
            area.BeginSectionDrag(first.Id, grab); area.ContinueDrag(end); app.WindowAtPoint = _ => IntPtr.Zero;
            app.UpdatePanelDrag(area, end);
            check(area.SectionDropIndex is null, "A covering application clears the internal reorder target");
            area.EndDrag(end); await Task.Delay(100);
            check(Order().SequenceEqual(original), "Releasing over another application cannot reorder groups underneath it");
            app.WindowAtPoint = _ => parent;
            grab = OnHeader(middle.Id, 20); top = OnHeader(first.Id, 5);
            area.BeginSectionDrag(middle.Id, grab); area.ContinueDrag(top); area.EndDrag(top); await Task.Delay(150);
            var persisted = new WorkspaceStore(Path.Combine(directory, "workspace.json")).Load();
            check(persisted.Panels.Select(p => p.Id).SequenceEqual(new[] { middle.Id, first.Id, last.Id }) && persisted.Panels[0].IsPinned,
                "Sorting a pinned group saves the new title order without unpinning it");
            app.RebuildPanels(); area = app.DesktopPanels.Single(); await Task.Delay(100);
            check(area.Sections.Select(s => s.Model.Id).SequenceEqual(new[] { middle.Id, first.Id, last.Id }) && area.IsSectionExpanded(middle.Id),
                "Recreated desktop panels restore the sorted titles and pinned section");
            grab = OnHeader(first.Id, 20); var outside = new DesktopNative.Point(170, 600);
            area.BeginSectionDrag(first.Id, grab); area.ContinueDrag(outside);
            check(area.SectionDropIndex is null, "Moving outside the accordion switches from sorting to detachment");
            area.EndDrag(outside); await Task.Delay(150);
            check(first.StackId is null && middle.StackId == last.StackId && app.DesktopPanels.Count() == 2 && first.Items.Count == 1,
                "A reordered group still detaches normally when dropped outside");
        }
        finally { app.WindowAtPoint = DesktopNative.WindowFromPoint; }
    }
}
