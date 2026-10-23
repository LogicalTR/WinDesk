using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Interop;
using WinDesk.Core;
using Forms = System.Windows.Forms;

namespace WinDesk.App;

public partial class App : Application
{
    private Mutex? _mutex;
    private SingleInstanceCommands? _commands;
    private Forms.NotifyIcon? _tray;
    private DesktopWindows? _desktop;
    private PanelHotkey? _panelHotkey;
    private SettingsWindow? _manager;
    private DesktopPanel? _dragSource;
    private WorkspaceStore _store = null!;
    private ShortcutImporter _shortcutImporter = null!;
    private readonly Dictionary<Guid, DesktopPanel> _windows = new();
    public Workspace Workspace { get; private set; } = null!;
    public bool IsExiting { get; private set; }
    public bool PanelsHidden { get; private set; }
    internal bool PanelsOnTop => _desktop?.PanelsOnTop == true;
    public event Action? WorkspaceChanged;
    public event Action? DesktopStatusChanged;
    public string DesktopStatus => _desktop?.Status ?? "Masaüstüne bağlanılıyor…";
    internal IEnumerable<DesktopPanel> DesktopPanels => _windows.Values;
    internal Func<DesktopNative.Point, IntPtr> WindowAtPoint { get; set; } = DesktopNative.WindowFromPoint;
    internal Action<ShortcutModel> ShortcutLauncher { get; set; } = Launch;
    internal Func<ProcessStartInfo, Process?> UpdateInstallerLauncher { get; set; } = Process.Start;
    internal void RefreshDesktopConnection() => _desktop?.Synchronize();
    public static App CurrentApp => (App)Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        // The portable EXE has the same minimum OS as Setup. Check before
        // opening the workspace or creating any Explorer child windows.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19045))
        {
            MessageBox.Show("WinDesk için Windows 10 22H2 (19045) veya üzeri gerekir.",
                "WinDesk", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(1); return;
        }
        try {
            if (DesktopLaunch.RelaunchIfRedirected(e.Args)) { Shutdown(); return; }
            DesktopLaunch.EnsureUnpackaged();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException) {
            MessageBox.Show(ex.Message, "WinDesk"); Shutdown(1); return;
        }
        base.OnStartup(e);
        // Setup's optional download is a separate process. It must also work
        // when the regular WinDesk instance is already running, without opening its workspace.
        if (Array.IndexOf(e.Args, "--download-bybrowser") >= 0)
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow = new ByBrowserDownloadWindow();
            MainWindow.Show();
            return;
        }
        int commandFlag = Array.IndexOf(e.Args, "--command");
        string? startupCommand = commandFlag >= 0 && commandFlag + 1 < e.Args.Length ? e.Args[commandFlag + 1] : null;
        if (commandFlag >= 0 && (startupCommand is null || !SingleInstanceCommands.IsValid(startupCommand))) { Shutdown(2); return; }
        _mutex = new Mutex(true, "Local\\WinDesk.Desktop.v1", out bool firstInstance);
        if (!firstInstance)
        {
            bool delivered = SingleInstanceCommands.SendAsync(startupCommand ?? "settings").GetAwaiter().GetResult();
            if (!delivered) MessageBox.Show("Çalışan WinDesk'e ulaşılamadı. Birkaç saniye sonra yeniden deneyin.", "WinDesk");
            Shutdown(delivered ? 0 : 1); return;
        }
        var dataDirectory = UserDataLocation.DirectoryPath;
        // Isolated state for development smoke tests; normal runs always use LocalAppData.
        int dataFlag = Array.IndexOf(e.Args, "--data-dir");
        if (dataFlag >= 0 && dataFlag + 1 < e.Args.Length) dataDirectory = Path.GetFullPath(e.Args[dataFlag + 1]);
        _store = new WorkspaceStore(Path.Combine(dataDirectory, "workspace.json"));
        try {
            if (dataFlag < 0) UserDataLocation.RecoverLegacyWorkspace(_store.FilePath);
            Workspace = _store.Load();
            _shortcutImporter = new ShortcutImporter(Path.Combine(dataDirectory, "Shortcuts"));
            if (_shortcutImporter.UpgradeExisting(Workspace) > 0)
            {
                if (File.Exists(_store.FilePath)) File.Copy(_store.FilePath, _store.FilePath + $".before-shortcut-import-{DateTime.Now:yyyyMMdd-HHmmssfff}.bak");
                _store.Save(Workspace);
            }
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            MessageBox.Show($"Düzen okunamadı; dosyanıza dokunulmadı.\n\n{_store.FilePath}\n\n{ex.Message}", "WinDesk", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1); return;
        }
        _desktop = new DesktopWindows(() => _windows.Values, () => DesktopStatusChanged?.Invoke());
        RebuildPanels();
        try { _panelHotkey = new PanelHotkey(() => Dispatcher.BeginInvoke(TogglePanelLayer)); }
        catch (System.ComponentModel.Win32Exception ex) { Trace.WriteLine($"WinDesk panel hotkey: {ex}"); }
        _commands = new SingleInstanceCommands(command => Dispatcher.BeginInvoke(() => ExecuteCommand(command)));
        var trayMenu = new Forms.ContextMenuStrip();
        trayMenu.Items.Add("Ayarlar", null, (_, _) => Dispatcher.Invoke(OpenManager));
        trayMenu.Items.Add("Yeni panel", null, (_, _) => Dispatcher.Invoke(CreatePanel));
        trayMenu.Items.Add("Panelleri gizle / göster", null, (_, _) => Dispatcher.Invoke(TogglePanels));
        trayMenu.Items.Add("Öne getir / arkaya gönder (Ctrl + Shift)", null, (_, _) => Dispatcher.Invoke(TogglePanelLayer));
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        trayMenu.Items.Add("Çıkış", null, (_, _) => Dispatcher.Invoke(ExitApp));
        _tray = new Forms.NotifyIcon { Text = "WinDesk · Masaüstü panellerin", Icon = System.Drawing.SystemIcons.Application, ContextMenuStrip = trayMenu, Visible = true };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(OpenManager);
        if (startupCommand is not null) Dispatcher.BeginInvoke(() => ExecuteCommand(startupCommand));
        int diagnosticsFlag = Array.IndexOf(e.Args, "--diagnostics");
        if (diagnosticsFlag >= 0 && diagnosticsFlag + 1 < e.Args.Length)
        {
            string diagnosticsPath = Path.GetFullPath(e.Args[diagnosticsFlag + 1]);
            var diagnosticTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            diagnosticTimer.Tick += (_, _) =>
            {
                diagnosticTimer.Stop();
                File.WriteAllText(diagnosticsPath, $"Windows version: {Environment.OSVersion.Version}; Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}\nData file: {_store.FilePath}\nGroups: {Workspace.Panels.Count}; Shortcuts: {Workspace.Panels.Sum(p => p.Items.Count)}\n" + DesktopNative.DescribeDesktop() + string.Join(Environment.NewLine,
                    _windows.Values.Select(p => $"Panel {p.Id}: loaded={p.IsLoaded} visible={p.IsVisible} size={p.ActualWidth},{p.ActualHeight} desired={p.DesiredSize} opacity={p.Opacity} presentation={System.Windows.PresentationSource.FromVisual(p) is not null}")));
            };
            diagnosticTimer.Start();
        }
    }

    public void OpenManager()
    {
        _manager ??= new SettingsWindow();
        if (!_manager.IsVisible) _manager.Show();
        _manager.WindowState = WindowState.Normal;
        DesktopNative.ShowWindow(new WindowInteropHelper(_manager).Handle, 5);
        _manager.Activate();
    }

    internal void ExecuteCommand(string command)
    {
        if (IsExiting) return;
        switch (command)
        {
            case "new-group": CreatePanel(); break;
            case "toggle-panels": TogglePanels(); break;
            case "settings": OpenManager(); break;
        }
    }

    public void CreatePanel()
    {
        var dialog = new PanelEditor(null) { Owner = _manager is { IsVisible: true } ? _manager : null, ShowInTaskbar = _manager is not { IsVisible: true } };
        if (dialog.ShowDialog() != true) return;
        var work = SystemParameters.WorkArea;
        var offset = (Workspace.Panels.Count % 7) * 56;
        var panel = new PanelModel { Name = dialog.PanelName, Color = dialog.PanelColor, X = work.Left + 80 + offset, Y = work.Top + 80 + offset };
        Workspace.Panels.Add(panel);
        ShowPanel(panel);
        Save();
        RevealPanel(panel);
    }

    public void EditPanel(PanelModel panel)
    {
        var dialog = new PanelEditor(panel) { Owner = _manager is { IsVisible: true } ? _manager : null, ShowInTaskbar = _manager is not { IsVisible: true } };
        if (dialog.ShowDialog() != true) return;
        panel.Name = dialog.PanelName; panel.Color = dialog.PanelColor;
        SurfaceFor(panel).Refresh(); Save();
    }

    public void SetPanelPinned(PanelModel panel, bool pinned)
    {
        panel.IsPinned = pinned;
        SurfaceFor(panel).PinChanged(panel.Id);
        Save();
    }

    public void RemovePanel(PanelModel panel)
    {
        if (MessageBox.Show($"“{panel.Name}” paneli kaldırılsın mı?\nUygulamalarınız ve dosyalarınız silinmez.", "Paneli kaldır", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Workspace.RemovePanel(panel.Id); RebuildPanels(); Save();
    }

    private void ShowPanel(PanelModel panel)
    {
        var window = new DesktopPanel(new[] { panel });
        _windows.Add(panel.Id, window);
        if (PanelsHidden) window.Hide();
        _desktop?.Synchronize();
    }

    private DesktopPanel SurfaceFor(PanelModel panel) => _windows[panel.StackId ?? panel.Id];

    internal void RebuildPanels()
    {
        EndShortcutDrag(default, false);
        CancelPanelDrag();
        foreach (var window in _windows.Values) window.Close();
        _windows.Clear();
        foreach (var panel in Workspace.Panels.Where(p => p.StackId is null))
            _windows.Add(panel.Id, new DesktopPanel(new[] { panel }));
        foreach (var stack in Workspace.Stacks)
            _windows.Add(stack.Id, new DesktopPanel(Workspace.Panels.Where(p => p.StackId == stack.Id), stack));
        if (PanelsHidden) foreach (var window in _windows.Values) window.Hide();
        _desktop?.Synchronize();
    }

    public void CombinePanels(IEnumerable<Guid> ids)
    {
        var selected = ids.ToArray();
        if (selected.Length < 2) return;
        var dialog = new PanelEditor(new PanelModel { Name = "Birleşik alan" }, "Grupları birleştir", false)
            { Owner = _manager is { IsVisible: true } ? _manager : null, ShowInTaskbar = _manager is not { IsVisible: true } };
        if (dialog.ShowDialog() != true) return;
        try { Workspace.Combine(selected, dialog.PanelName); RebuildPanels(); Save(); }
        catch (ArgumentException ex) { ShowError("Gruplar birleştirilemedi", ex); }
    }

    public void SeparateStack(Guid stackId)
    { Workspace.Separate(stackId); RebuildPanels(); Save(); }

    internal void StartPanelDrag(DesktopPanel source)
    {
        _dragSource = source;
        foreach (var panel in _windows.Values) panel.SetDragState(true, false, false);
    }

    internal DesktopPanel? FindDropTarget(DesktopPanel source, DesktopNative.Point point)
    {
        if (!source.IsDropAvailable) return null;
        // Mouse capture can continue over other apps. Never merge through a
        // window covering the desktop, even if a panel sits underneath it.
        if (!IsPanelSurfacePoint(source, point)) return null;
        DesktopPanel? best = null;
        double bestScore = 0;
        bool overlapAllowed = _dragSource == source && source.TryGetDragBounds(out _);
        source.TryGetDragBounds(out var moving);
        double scale = DesktopNative.GetDpiForWindow(source.Handle) / 96.0;
        foreach (var panel in _windows.Values)
        {
            if (panel == source || !panel.IsDropAvailable || !DesktopNative.GetWindowRect(panel.Handle, out var rect)) continue;
            bool underPointer = point.X >= rect.Left && point.X < rect.Right && point.Y >= rect.Top && point.Y < rect.Bottom;
            int left = Math.Max(rect.Left, moving.Left), top = Math.Max(rect.Top, moving.Top);
            int width = Math.Min(rect.Right, moving.Right) - left, height = Math.Min(rect.Bottom, moving.Bottom) - top;
            double ratio = width > 0 && height > 0 ? (double)width * height / Math.Max(1,
                Math.Min((double)(rect.Right - rect.Left) * (rect.Bottom - rect.Top), (double)(moving.Right - moving.Left) * (moving.Bottom - moving.Top))) : 0;
            bool overlapping = overlapAllowed && width >= 96 * scale && height >= 18 * scale && ratio >= 0.25 &&
                IsPanelSurfacePoint(source, new DesktopNative.Point(left + width / 2, top + height / 2));
            double score = underPointer ? 2 + ratio : overlapping ? ratio : 0;
            if (score > bestScore) { best = panel; bestScore = score; }
        }
        return best;
    }

    internal void UpdatePanelDrag(DesktopPanel source, DesktopNative.Point point)
    {
        if (_dragSource != source) return;
        bool onDesktop = source.IsDropAvailable && IsPanelSurfacePoint(source, point);
        if (onDesktop) source.ScrollSectionsForDrop(point);
        int? reorderIndex = onDesktop ? source.SectionInsertionIndex(point) : null;
        var target = FindDropTarget(source, point);
        bool canDetach = source.DraggedSectionId.HasValue && source.IsOutsideOriginalArea(point) && onDesktop;
        source.SetSectionDropHint(canDetach, canDetach && target is not null, reorderIndex);
        if (source.DraggedSectionId.HasValue && !canDetach) target = null;
        foreach (var panel in _windows.Values) panel.SetDragState(true, panel == target, panel == source && target is not null);
    }

    internal bool CompletePanelDrag(DesktopPanel source, DesktopNative.Point point)
    {
        if (_dragSource != source) return false;
        var target = FindDropTarget(source, point);
        Guid? sectionId = source.DraggedSectionId;
        int? reorderIndex = null;
        double x = source.DetachX, y = source.DetachY;
        if (sectionId.HasValue)
        {
            if (!source.IsDropAvailable ||
                !IsPanelSurfacePoint(source, point)) return false;
            reorderIndex = source.SectionInsertionIndex(point);
            if (reorderIndex.HasValue) target = null;
            else if (!source.IsOutsideOriginalArea(point)) return false;
        }
        else if (target is null) return false;
        CancelPanelDrag();
        // Dispose the old HWNDs after the mouse-up/capture callback has finished.
        Dispatcher.BeginInvoke(() =>
        {
            if (!_windows.TryGetValue(source.Id, out var currentSource) || currentSource != source) return;
            if (target is not null && (!_windows.TryGetValue(target.Id, out var currentTarget) || currentTarget != target)) return;
            if (sectionId is Guid id)
            {
                if (!Workspace.Panels.Any(p => p.Id == id && p.StackId == source.Id)) return;
                if (reorderIndex is int index)
                {
                    if (Workspace.ReorderPanel(id, index))
                    {
                        source.ApplySectionOrder(Workspace.Panels.Where(p => p.StackId == source.Id));
                        Save();
                    }
                    return;
                }
                Workspace.DetachPanel(id, x, y);
                if (target is not null) Workspace.MergeAreas(id, target.Id);
            }
            else Workspace.MergeAreas(source.Id, target!.Id);
            RebuildPanels(); Save();
        });
        return true;
    }

    internal void CancelPanelDrag()
    {
        _dragSource = null;
        foreach (var panel in _windows.Values) panel.SetDragState(false, false, false);
    }

    public void RenameStack(PanelStack stack)
    {
        var dialog = new PanelEditor(new PanelModel { Name = stack.Name }, "Birleşik alanı düzenle", false)
            { Owner = _manager is { IsVisible: true } ? _manager : null, ShowInTaskbar = _manager is not { IsVisible: true } };
        if (dialog.ShowDialog() != true) return;
        stack.Name = dialog.PanelName; _windows[stack.Id].Refresh(); Save();
    }

    public void RevealPanel(PanelModel panel)
    {
        if (PanelsHidden) TogglePanels();
        _desktop?.Synchronize();
        var window = SurfaceFor(panel);
        window.Show(); window.Expand(panel.Id);
    }

    public void TogglePanels()
    {
        EndShortcutDrag(default, false);
        PanelsHidden = !PanelsHidden;
        foreach (var window in _windows.Values)
        {
            if (PanelsHidden) window.Hide(); else window.Show();
        }
        WorkspaceChanged?.Invoke();
    }

    internal void TogglePanelLayer()
    {
        if (IsExiting || _desktop is null) return;
        EndShortcutDrag(default, false);
        CancelPanelDrag();
        _desktop.PanelsOnTop = !_desktop.PanelsOnTop;
        if (_desktop.PanelsOnTop && PanelsHidden) TogglePanels();
        _desktop.Synchronize();
    }

    private bool IsPanelSurfacePoint(DesktopPanel source, DesktopNative.Point point)
    {
        IntPtr root = DesktopNative.GetAncestor(WindowAtPoint(point), 2);
        return root != IntPtr.Zero && (root == source.DesktopParent ||
            _windows.Values.Any(panel => panel.IsTopmost && panel.IsDropAvailable && panel.Handle == root));
    }

    internal void SetCloseDelay(int milliseconds)
    {
        Workspace.CloseDelayMs = Math.Clamp(milliseconds, 200, 2000);
        foreach (var panel in _windows.Values) panel.RefreshCloseDelay();
    }

    public void AddFiles(PanelModel panel)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Uygulama, kısayol veya dosya ekle", Multiselect = true, Filter = "Tüm dosyalar|*.*", DereferenceLinks = false };
        if (dialog.ShowDialog() != true) return;
        AddPaths(panel, dialog.FileNames);
    }

    public void AddFolder(PanelModel panel)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Panele klasör ekle", Multiselect = true };
        if (dialog.ShowDialog() == true) AddPaths(panel, dialog.FolderNames);
    }

    public void AddPaths(PanelModel panel, IEnumerable<string> paths)
    {
        try
        {
            var imported = paths.Where(path => Path.IsPathFullyQualified(path) && (File.Exists(path) || Directory.Exists(path)))
                .Select(_shortcutImporter.Import).ToList();
            int added = 0;
            foreach (var shortcut in imported) if (panel.AddShortcut(shortcut)) added++;
            if (added == 0) return;
            SurfaceFor(panel).Refresh(); Save();
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        { ShowError("Kısayol eklenemedi", ex); }
    }

    public void RemoveShortcut(PanelModel panel, ShortcutModel shortcut)
    { panel.Items.Remove(shortcut); SurfaceFor(panel).Refresh(); Save(); }

    public void Save()
    {
        try { _store.Save(Workspace); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { ShowError("Düzen kaydedilemedi", ex); }
        WorkspaceChanged?.Invoke();
    }

    public static void Launch(ShortcutModel shortcut)
    {
        try
        {
            Process.Start(ShortcutImporter.StartInfo(shortcut));
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        { ShowError("Öğe açılamadı", ex); }
    }

    public static void ShowError(string title, Exception error) => MessageBox.Show(error.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    internal void InstallUpdate(ReleaseUpdate update, string installer)
    {
        using var verified = ReleaseUpdates.OpenVerifiedInstaller(update, installer);
        EndShortcutDrag(default, false);
        foreach (var window in _windows.Values) window.CapturePosition();
        // Unlike Save(), propagate failures: never quit for an update if saving failed.
        _store.Save(Workspace);
        using var process = UpdateInstallerLauncher(new ProcessStartInfo(installer, "/SP- /NORESTART /WINDESKUPDATE=1") { UseShellExecute = false })
            ?? throw new InvalidOperationException("Kurulum başlatılamadı.");
        IsExiting = true;
        _desktop?.Dispose();
        foreach (var window in _windows.Values) window.Close();
        _tray?.Dispose(); Shutdown();
    }

    public void ExitApp()
    {
        EndShortcutDrag(default, false);
        IsExiting = true;
        foreach (var window in _windows.Values) window.CapturePosition();
        Save(); _desktop?.Dispose();
        foreach (var window in _windows.Values) window.Close();
        _tray?.Dispose(); Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    { _panelHotkey?.Dispose(); _commands?.Dispose(); _desktop?.Dispose(); _tray?.Dispose(); _mutex?.Dispose(); base.OnExit(e); }
}
