using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinDesk.App;

internal static class Program
{
    private static int _assertions;
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--installed-update-test") {
            try { return InstalledUpdateTests.Run(Path.GetFullPath(args[1])); }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.Length == 2 && args[0] == "--update-test") {
            try { UpdatePanelTests.Run(Check, Path.GetFullPath(args[1])); Console.WriteLine($"{_assertions} background update UI assertions passed."); return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args.Length >= 2 && args[0] == "--shortcut-probe") {
            // Publish only a completed response: on a slower VM the caller can
            // observe File.Exists while WriteAllText still holds an exclusive handle.
            string temporary = args[1] + ".tmp";
            File.WriteAllText(temporary, System.Text.Json.JsonSerializer.Serialize(new { Arguments = args.Skip(2), Directory = Environment.CurrentDirectory }));
            File.Move(temporary, args[1]); return 0;
        }
        if (args.Contains("--import-test")) { ShortcutImportTests.Run(Check).GetAwaiter().GetResult(); Console.WriteLine($"{_assertions} shortcut import assertions passed."); return 0; }
        if (args.Contains("--preview-desktop")) return DesktopPreview.Run();
        if (args.Contains("--inspect-desktop")) { Console.WriteLine(DesktopNative.DescribeDesktop()); return 0; }
        if (args.Contains("--recovery-test")) { RecoveryTests.Run(Check); Console.WriteLine($"{_assertions} recovery assertions passed."); return 0; }
        if (!args.Contains("--data-dir") || !args.Contains("--quiet"))
            throw new ArgumentException("UI tests require an isolated --data-dir and --quiet.");
        int exitCode = 0;
        System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.ConsoleTraceListener());
        var app = new App();
        app.InitializeComponent();
        if (args.Contains("--interactive-drag-test"))
        {
            app.Startup += (_, _) => app.Dispatcher.BeginInvoke(() => DragInputHarness.Open(app, args[Array.IndexOf(args, "--data-dir") + 1], args.Contains("--scroll-test")));
            return app.Run();
        }
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                if (args.Contains("--explorer-restart-test")) {
                    await ExplorerRestartTests.Run(app, Check);
                    Console.WriteLine($"{_assertions} real Explorer restart assertions passed."); return;
                }
                if (args.Contains("--display-test")) {
                    await DisplayBehaviorTests.Run(app, Check);
                    Console.WriteLine($"{_assertions} display behavior assertions passed."); return;
                }
                var panel = app.DesktopPanels.First();
                var first = app.Workspace.Panels[0];
                if (panel.Handle == IntPtr.Zero) Console.WriteLine(DesktopNative.DescribeDesktop() + app.DesktopStatus);
                Check(panel.Handle != IntPtr.Zero, "A desktop child HWND is created on the live Explorer desktop");
                Check(DesktopNative.IsDesktopHost(panel.DesktopParent), "Parent is a verified Explorer desktop host");
                Check(DesktopNative.GetParent(panel.Handle) == panel.DesktopParent, "Native parent is the desktop, not an owner relationship");
                long style = DesktopNative.GetWindowLongPtr(panel.Handle, -16).ToInt64();
                Check((style & 0x40000000) != 0 && (style & 0x80000000L) == 0, "Panel has WS_CHILD and no top-level WS_POPUP style");
                Check((DesktopNative.GetWindowLongPtr(panel.Handle, -20).ToInt64() & 0x08) == 0, "Panel never has the always-on-top style");
                Check(DesktopNative.GetLayeredWindowAttributes(panel.Handle, out _, out byte alpha, out uint layeredFlags) && alpha == 255 && (layeredFlags & 2) != 0,
                    "Desktop container has an initialized opaque composition surface");
                var presentation = (HwndSource)PresentationSource.FromVisual(panel)!;
                Check(!presentation.UsesPerPixelOpacity && DesktopNative.GetParent(presentation.Handle) == panel.Handle,
                    "WPF paints through a regular child inside the desktop composition container");
                Check(!app.Windows.OfType<Window>().Any(w => w.Title.StartsWith("WinDesk ·")), "Panels are not WPF top-level windows or Alt-Tab entries");
                panel.Hide(); // Keep real pointer activity independent of this dispatcher test.
                await Task.Delay(100);
                Check(Math.Abs(panel.Height - 46) < 1, "Panels start as compact title bars");
                panel.HoverSection(first.Id);
                await Task.Delay(360);
                Check(panel.Height > 100, "Mouse-enter event opens the panel after its delay");
                panel.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
                await Task.Delay(900);
                Check(Math.Abs(panel.Height - 46) < 1, "Mouse-leave event collapses the panel after its delay");
                panel.HoverSection(first.Id);
                panel.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseLeaveEvent });
                await Task.Delay(300);
                Check(Math.Abs(panel.Height - 46) < 1, "Brief pointer crossings do not flash the panel open");
                panel.Expand();
                await Task.Delay(900);
                Check(Math.Abs(panel.Height - 46) < 1, "Show action also returns to a title bar when the pointer is elsewhere");
                panel.Show();
                DesktopNative.GetWindowRect(panel.Handle, out var rect);
                var cover = new Window { Title = "WinDesk integration test", Width = 500, Height = 400, ShowActivated = true, ShowInTaskbar = false, Background = Brushes.DarkSlateGray };
                try
                {
                    cover.Show();
                    var coverHandle = new WindowInteropHelper(cover).Handle;
                    DesktopNative.SetWindowPos(coverHandle, IntPtr.Zero, rect.Left - 12, rect.Top - 12, 500, 400, DesktopNative.NoActivate);
                    cover.Activate();
                    await Task.Delay(150);
                    var point = new DesktopNative.Point(rect.Left + 24, rect.Top + 22);
                    // A user's foreground app may legitimately stay above the
                    // non-topmost fixture. Either application must cover panels.
                    var blockingRoot = DesktopNative.GetAncestor(DesktopNative.WindowFromPoint(point), 2);
                    Check(blockingRoot != IntPtr.Zero && blockingRoot != panel.DesktopParent, "An application window covers the desktop panel");
                    var foreground = DesktopNative.GetForegroundWindow();
                    app.RevealPanel(first); app.RefreshDesktopConnection();
                    await Task.Delay(300);
                    Check(DesktopNative.GetAncestor(DesktopNative.WindowFromPoint(point), 2) == blockingRoot, "Opening and reconnecting a panel cannot raise it above another window");
                    Check(DesktopNative.GetForegroundWindow() == foreground, "Panel reveal and recovery do not steal foreground focus");
                    Check(app.FindDropTarget(panel, point) is null, "Drag merge cannot target panels through an ordinary application window");
                }
                finally { cover.Close(); }

                panel.MoveTo(180, 140); panel.CapturePosition();
                double savedX = first.X, savedY = first.Y;
                IntPtr staleHandle = panel.Handle;
                panel.DisconnectFromDesktop();
                Check(!DesktopNative.IsWindow(staleHandle), "Lost native child resources are released");
                app.RefreshDesktopConnection();
                Check(panel.Handle != IntPtr.Zero && DesktopNative.GetParent(panel.Handle) == panel.DesktopParent && first.X == savedX && first.Y == savedY,
                    "A lost child is recreated on the desktop without losing position");
                using (var unavailable = new DesktopWindows(() => new[] { panel }, () => { }, () => IntPtr.Zero))
                {
                    unavailable.Synchronize();
                    Check(panel.Handle == IntPtr.Zero && !unavailable.IsConnected, "Missing desktop leaves panels absent instead of floating over applications");
                }
                app.RefreshDesktopConnection();
                Check(panel.Handle != IntPtr.Zero, "Desktop reconnection restores a panel after temporary unavailability");
                app.TogglePanels(); panel.DisconnectFromDesktop(); app.RefreshDesktopConnection();
                Check(panel.Handle != IntPtr.Zero && !DesktopNative.IsWindowVisible(panel.Handle), "Recovery preserves the user's hidden-panel preference");
                app.TogglePanels();

                var second = app.Workspace.Panels[1];
                var combined = app.Workspace.Combine(new[] { first.Id, second.Id }, "Akordiyon testi");
                app.RebuildPanels();
                var accordion = app.DesktopPanels.Single();
                Check(accordion.ClosedHeight == 90 && accordion.Handle != IntPtr.Zero, "Two groups share one desktop HWND and two compact headers");
                accordion.Hide(); accordion.Expand(first.Id);
                await Task.Delay(350);
                Check(accordion.ActiveSectionId == first.Id && Math.Abs(accordion.Height - 232) < 2, "First accordion section expands inside the shared area");
                accordion.Expand(second.Id);
                await Task.Delay(350);
                Check(accordion.ActiveSectionId == second.Id && Math.Abs(accordion.Height - 232) < 2, "Opening another section closes the previous one without growing a second body");
                accordion.MoveTo(300, 160); accordion.CapturePosition();
                Check(combined.X == 300 && combined.Y == 160, "Dragging a combined area moves all sections through one shared position");
                accordion.Collapse(); await Task.Delay(250);
                Check(Math.Abs(accordion.Height - 90) < 1, "Closing an accordion keeps every group header visible");
                DesktopNative.GetWindowRect(accordion.Handle, out var collapsedBounds);
                Check(collapsedBounds.Bottom - collapsedBounds.Top == (int)Math.Ceiling(90 * DesktopNative.GetDpiForWindow(accordion.Handle) / 96.0),
                    "The native accordion window shrinks to exactly its visible headers");
                string previewDirectory = args[Array.IndexOf(args, "--data-dir") + 1];
                WritePreview(accordion, Path.Combine(previewDirectory, "accordion-closed.png"));
                first.AddPaths(new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe") });
                accordion.Refresh(); accordion.Expand(first.Id); await Task.Delay(250);
                WritePreview(accordion, Path.Combine(previewDirectory, "accordion-open.png"));
                app.Workspace.Separate(combined.Id); app.RebuildPanels();
                Check(app.DesktopPanels.Count() == 2 && app.DesktopPanels.All(p => p.Handle != IntPtr.Zero && DesktopNative.GetParent(p.Handle) == p.DesktopParent),
                    "Separated groups return as independent desktop children");
                var dragSource = app.DesktopPanels.First();
                var dropTarget = app.DesktopPanels.Last();
                dragSource.MoveTo(60, 60); dropTarget.MoveTo(600, 250);
                dragSource.Show(); dropTarget.Show();
                await Task.Delay(150);
                DesktopNative.GetWindowRect(dragSource.Handle, out var dragRect);
                DesktopNative.GetWindowRect(dropTarget.Handle, out var dropRect);
                var startPoint = new DesktopNative.Point(dragRect.Left + 80, dragRect.Top + 22);
                var targetPoint = new DesktopNative.Point(dropRect.Left + 80, dropRect.Top + 22);
                // Inject just the native hit-test boundary so real user windows and
                // pointer movement cannot disturb the synthetic drag sequence.
                // The real covering-window check above uses the actual Win32 hit test.
                app.WindowAtPoint = _ => dropTarget.DesktopParent;
                try
                {
                    dragSource.BeginDrag(startPoint);
                    dragSource.ContinueDrag(new DesktopNative.Point(startPoint.X + 1, startPoint.Y + 1));
                    dragSource.EndDrag(startPoint);
                    Check(dragSource.ScreenX == 60 && dragSource.ScreenY == 60 && app.Workspace.Stacks.Count == 0,
                        "A title click or tiny pointer movement cannot move or merge a group");
                    dragSource.BeginDrag(startPoint); dragSource.ContinueDrag(targetPoint);
                    Check(app.FindDropTarget(dragSource, targetPoint) == dropTarget, "Dragging recognizes the destination area under the pointer");
                    WritePreview(dragSource, Path.Combine(previewDirectory, "drag-merge-hint.png"));
                    dragSource.EndDrag(targetPoint, commit: false);
                    await Task.Delay(100);
                    Check(app.Workspace.Stacks.Count == 0 && app.DesktopPanels.Count() == 2,
                        "Losing mouse capture cancels merging and preserves both groups");
                    dragSource.MoveTo(60, 60);
                    dropTarget.Hide();
                    Check(app.FindDropTarget(dragSource, targetPoint) is null, "Hidden areas cannot receive a merge drop");
                    dropTarget.Show();
                    Check(app.FindDropTarget(dragSource, new DesktopNative.Point(-10000, -10000)) is null,
                        "Dropping outside every area does not select a destination");
                    dragSource.BeginDrag(startPoint); dragSource.ContinueDrag(targetPoint); dragSource.EndDrag(targetPoint);
                    await Task.Delay(250);
                    Check(app.Workspace.Stacks.Count == 1 && app.DesktopPanels.Count() == 1 && app.Workspace.Stacks[0].X == 600 && app.Workspace.Stacks[0].Y == 250,
                        "A completed header drag creates one accordion at the destination position");
                    Check(app.Workspace.Panels.Sum(p => p.Items.Count) == 2 && app.DesktopPanels.Single().ClosedHeight == 90,
                        "Drag-created accordion retains both headers and all shortcuts");
                }
                finally { app.WindowAtPoint = DesktopNative.WindowFromPoint; }
                var pinnedArea = app.DesktopPanels.Single();
                pinnedArea.Hide();
                var pinSection = Descendants<PanelSection>(pinnedArea).Single(s => s.Model.Id == first.Id);
                var pinButton = (System.Windows.Controls.Primitives.ToggleButton)pinSection.FindName("PinButton");
                pinButton.IsChecked = true;
                pinButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                pinnedArea.Collapse(); await Task.Delay(900);
                Check(first.IsPinned && pinnedArea.IsSectionExpanded(first.Id), "The header pin button keeps the group open after the close delay");
                pinnedArea.Expand(second.Id); await Task.Delay(250);
                Check(pinnedArea.IsSectionExpanded(first.Id) && pinnedArea.IsSectionExpanded(second.Id), "Opening another section does not close a pinned group");
                app.SetPanelPinned(second, true); pinnedArea.Collapse(); await Task.Delay(900);
                Check(pinnedArea.IsSectionExpanded(first.Id) && pinnedArea.IsSectionExpanded(second.Id), "Multiple pinned groups remain open together after leaving the area");
                WritePreview(pinnedArea, Path.Combine(previewDirectory, "pinned-accordion.png"));
                app.SetPanelPinned(second, false); pinnedArea.Collapse(); await Task.Delay(250);
                Check(pinnedArea.IsSectionExpanded(first.Id) && !pinnedArea.IsSectionExpanded(second.Id), "Unpinning restores automatic collapse without affecting other pinned groups");
                app.RebuildPanels(); pinnedArea = app.DesktopPanels.Single();
                Check(pinnedArea.IsSectionExpanded(first.Id) && !pinnedArea.IsSectionExpanded(second.Id), "Pinned sections reopen when native panels are rebuilt");
                pinnedArea.Show(); pinnedArea.MoveTo(600, 250); pinnedArea.UpdateLayout(); await Task.Delay(100);
                var parent = pinnedArea.DesktopParent;
                app.WindowAtPoint = _ => parent;
                try
                {
                    var header = Descendants<PanelSection>(pinnedArea).Single(s => s.Model.Id == first.Id).HeaderScreenOrigin;
                    var grab = new DesktopNative.Point((int)header.X + 85, (int)header.Y + 20);
                    // Keep this offset test clear of the work-area boundary on
                    // the 1280x800 VM; screen-edge clamping is valid behavior.
                    var outside = new DesktopNative.Point(150, 350);
                    pinnedArea.BeginSectionDrag(first.Id, grab); pinnedArea.ContinueDrag(outside);
                    Check(pinnedArea.ScreenX == 600 && pinnedArea.ScreenY == 250, "Dragging a member out leaves the original shared area in place");
                    pinnedArea.EndDrag(outside, commit: false); await Task.Delay(100);
                    Check(first.StackId.HasValue && app.DesktopPanels.Count() == 1, "Interrupted section drags do not detach or lose a group");
                    var inside = new DesktopNative.Point(grab.X + 20, grab.Y);
                    pinnedArea.BeginSectionDrag(first.Id, grab); pinnedArea.ContinueDrag(inside); pinnedArea.EndDrag(inside); await Task.Delay(100);
                    Check(first.StackId.HasValue && app.DesktopPanels.Count() == 1, "Dropping back inside the original area cancels detachment");
                    app.WindowAtPoint = _ => IntPtr.Zero;
                    pinnedArea.BeginSectionDrag(first.Id, grab); pinnedArea.ContinueDrag(outside); pinnedArea.EndDrag(outside); await Task.Delay(100);
                    Check(first.StackId.HasValue, "A section cannot detach through a non-desktop window");
                    app.WindowAtPoint = _ => parent;
                    pinnedArea.BeginSectionDrag(first.Id, grab); pinnedArea.ContinueDrag(outside); pinnedArea.EndDrag(outside); await Task.Delay(300);
                    var detached = app.DesktopPanels.Single(p => p.Contains(first.Id));
                    var survivor = app.DesktopPanels.Single(p => p.Contains(second.Id));
                    Check(first.StackId is null && second.StackId is null && app.Workspace.Stacks.Count == 0 && app.DesktopPanels.Count() == 2,
                        "Dropping a section outside creates independent panels and dissolves a one-member area");
                    Check(detached.IsSectionExpanded(first.Id) && first.IsPinned && first.Items.Count == 2 && survivor.ScreenX == 600 && survivor.ScreenY == 250,
                        "Detachment preserves the pinned open state, shortcuts and remaining area position");
                    Check(Math.Abs(detached.ScreenX - 65) < 2 && Math.Abs(detached.ScreenY - 330) < 2,
                        "Detached panel follows the grabbed header offset at the drop position");
                    var newArea = app.Workspace.MergeAreas(first.Id, second.Id);
                    var thirdGroup = new WinDesk.Core.PanelModel { Name = "Üçüncü grup", X = 1100, Y = 400 };
                    app.Workspace.Panels.Add(thirdGroup); app.RebuildPanels(); await Task.Delay(150);
                    var sourceArea = app.DesktopPanels.Single(p => p.Id == newArea.Id);
                    var targetArea = app.DesktopPanels.Single(p => p.Id == thirdGroup.Id);
                    var sourceHeader = Descendants<PanelSection>(sourceArea).Single(s => s.Model.Id == first.Id).HeaderScreenOrigin;
                    var sectionGrab = new DesktopNative.Point((int)sourceHeader.X + 85, (int)sourceHeader.Y + 20);
                    DesktopNative.GetWindowRect(targetArea.Handle, out var targetBounds);
                    var ontoTarget = new DesktopNative.Point(targetBounds.Left + 85, targetBounds.Top + 20);
                    sourceArea.BeginSectionDrag(first.Id, sectionGrab); sourceArea.ContinueDrag(ontoTarget); sourceArea.EndDrag(ontoTarget); await Task.Delay(300);
                    Check(first.StackId == thirdGroup.StackId && first.StackId.HasValue && second.StackId is null && app.Workspace.Stacks.Count == 1,
                        "A member dragged onto another area moves only that group into the destination");
                    Check(first.IsPinned && app.DesktopPanels.Single(p => p.Contains(first.Id)).IsSectionExpanded(first.Id),
                        "Moving a pinned member between areas keeps it open");
                    var persisted = new WinDesk.Core.WorkspaceStore(Path.Combine(previewDirectory, "workspace.json")).Load();
                    Check(persisted.Panels.Single(p => p.Id == first.Id).IsPinned && persisted.Panels.Single(p => p.Id == first.Id).StackId == first.StackId,
                        "Pin-button and section-drop actions save their final state to disk");
                }
                finally { app.WindowAtPoint = DesktopNative.WindowFromPoint; }
                await ShortcutTests.Run(app, previewDirectory, Check);
                await SectionOrderTests.Run(app, previewDirectory, Check);
                await CommandTests.Run(app, Check);
                RecoveryTests.Run(Check);
                await ShortcutImportTests.Run(Check);
                await DisplayBehaviorTests.Run(app, Check);
                Console.WriteLine($"{_assertions} WPF and desktop integration assertions passed.");
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); exitCode = 1; }
            finally { app.ExitApp(); }
        });
        app.Run();
        return exitCode;
    }

    private static void WritePreview(DesktopPanel panel, string path)
    {
        panel.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(panel.Width), (int)Math.Ceiling(panel.Height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(panel);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path); encoder.Save(stream);
        Console.WriteLine("UI preview: " + path);
    }

    private static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _assertions++;
        Console.WriteLine("PASS " + message);
    }
}
