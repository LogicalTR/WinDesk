using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WinDesk.Core;

namespace WinDesk.App;

/// <summary>A single desktop HWND containing one group or a mutually exclusive accordion.</summary>
public sealed class DesktopPanel : UserControl, IDisposable
{
    private readonly List<PanelModel> _models;
    private readonly PanelStack? _stack;
    private readonly StackPanel _sectionsLayout = new();
    private readonly ScrollViewer _viewport;
    private readonly Border _frame;
    private readonly Border _dropHint;
    private readonly Canvas _sectionDropOverlay = new() { IsHitTestVisible = false, ClipToBounds = true };
    private readonly Border _sectionDropLine = new() { Height = 3, CornerRadius = new CornerRadius(1), Background = ManagerWindow.Brush("#93E7D5"), Visibility = Visibility.Collapsed };
    private readonly Border _sectionDropLabel = new()
    {
        Background = ManagerWindow.Brush("#28554B"), CornerRadius = new CornerRadius(4), Padding = new Thickness(7, 3, 7, 3), Visibility = Visibility.Collapsed,
        Child = new TextBlock { Text = "Buraya taşı", Foreground = Brushes.White, FontSize = 11 }
    };
    private readonly List<PanelSection> _sections = new();
    private readonly List<ContextMenu> _menus = new();
    private readonly DispatcherTimer _openTimer = new() { Interval = TimeSpan.FromMilliseconds(140) };
    private readonly DispatcherTimer _closeTimer = new();
    private readonly DispatcherTimer _positionTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly DispatcherTimer _sectionScrollTimer = new() { Interval = TimeSpan.FromMilliseconds(70) };
    private DesktopPanelHost? _host;
    private DesktopPanelHost? _dragPreview;
    private TextBlock? _dragPreviewHint;
    private Guid? _draggedSectionId;
    private DesktopNative.Rect _originalBounds;
    private double _grabX, _grabY, _detachX, _detachY;
    private Guid? _pendingSection;
    private Guid? _shortcutSourceSectionId;
    private bool _closed, _dragging, _dragMoved, _anotherPanelDragging, _dialogOpen, _requestedVisible = true;
    private int _openMenus;
    private DesktopNative.Point _dragStart;
    private double _dragX, _dragY;
    private double _screenX, _screenY;
    private App Controller => App.CurrentApp;
    internal IntPtr Handle => _host?.Handle ?? IntPtr.Zero;
    internal IntPtr DesktopParent => _host?.Parent ?? IntPtr.Zero;
    internal Guid Id => _stack?.Id ?? _models[0].Id;
    internal Guid? DraggedSectionId => _draggedSectionId;
    internal double DetachX => _detachX;
    internal double DetachY => _detachY;
    internal bool IsSectionExpanded(Guid id) => _sections.Single(s => s.Model.Id == id).IsExpanded;
    internal bool IsDropAvailable => !_closed && _requestedVisible && _host is { IsAlive: true } && DesktopNative.IsWindowVisible(Handle);
    internal IEnumerable<PanelSection> Sections => _sections;
    internal Func<System.Drawing.Point, System.Drawing.Rectangle> WorkingAreaAt { get; set; } =
        point => System.Windows.Forms.Screen.FromPoint(point).WorkingArea;
    internal bool TryGetDragBounds(out DesktopNative.Rect bounds) =>
        DesktopNative.GetWindowRect(_draggedSectionId.HasValue ? _dragPreview?.Handle ?? IntPtr.Zero : Handle, out bounds);
    internal Guid? ActiveSectionId { get; private set; }
    internal int? SectionDropIndex { get; private set; }
    internal double ClosedHeight => 2 + _models.Count * 44;
    internal double ScreenX => _screenX;
    internal double ScreenY => _screenY;
    private double SavedX { get => _stack?.X ?? _models[0].X; set { if (_stack is not null) _stack.X = value; else _models[0].X = value; } }
    private double SavedY { get => _stack?.Y ?? _models[0].Y; set { if (_stack is not null) _stack.Y = value; else _models[0].Y = value; } }

    internal DesktopPanel(IEnumerable<PanelModel> models, PanelStack? stack = null)
    {
        _models = models.ToList(); _stack = stack;
        _screenX = SavedX; _screenY = SavedY;
        Width = 336; Height = ClosedHeight;
        Foreground = ManagerWindow.Brush("#EDF1F7");
        FontFamily = new FontFamily("Segoe UI");
        _viewport = new ScrollViewer
        {
            Content = _sectionsLayout, Padding = new Thickness(0), BorderThickness = new Thickness(0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        _frame = new Border
        {
            Background = ManagerWindow.Brush("#F21A2230"), BorderBrush = ManagerWindow.Brush("#465568"),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), ClipToBounds = true,
            Child = _viewport
        };
        _dropHint = new Border
        {
            Visibility = Visibility.Collapsed, IsHitTestVisible = false,
            Background = ManagerWindow.Brush("#EE28554B"), CornerRadius = new CornerRadius(7),
            Margin = new Thickness(5), Padding = new Thickness(10, 5, 10, 5),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock { Text = "Birleştirmek için bırak", Foreground = Brushes.White, FontSize = 12 }
        };
        _sectionDropOverlay.Children.Add(_sectionDropLine); _sectionDropOverlay.Children.Add(_sectionDropLabel);
        var layout = new Grid(); layout.Children.Add(_frame); layout.Children.Add(_dropHint); layout.Children.Add(_sectionDropOverlay); Content = layout;
        foreach (var model in _models)
        {
            var section = new PanelSection(model, this);
            _sections.Add(section); _sectionsLayout.Children.Add(section);
        }
        ApplyExpandedState(animate: false);
        _openTimer.Tick += (_, _) => { _openTimer.Stop(); if (_pendingSection is Guid id) Expand(id); };
        _closeTimer.Tick += (_, _) => { _closeTimer.Stop(); if (!IsMouseOver && !_dragging && !_anotherPanelDragging && !_dialogOpen && _openMenus == 0) Collapse(); };
        _positionTimer.Tick += (_, _) => { _positionTimer.Stop(); if (!_closed) Controller.Save(); };
        _sectionScrollTimer.Tick += (_, _) =>
        {
            if (_dragging && _draggedSectionId.HasValue && DesktopNative.GetCursorPos(out var point)) Controller.UpdatePanelDrag(this, point);
        };
        MouseEnter += (_, _) => _closeTimer.Stop();
        MouseLeave += (_, _) => { _openTimer.Stop(); _pendingSection = null; QueueCollapse(); };
        SizeChanged += (_, _) => UpdateBounds();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            if (_dragging) { EndDrag(commit: false); Mouse.Capture(null); }
            else Collapse();
            e.Handled = true;
        };
    }

    internal bool Contains(Guid panelId) => _models.Any(p => p.Id == panelId);

    internal void ConnectToDesktop(IntPtr desktop)
    {
        if (_closed) return;
        if (_host is null || !_host.IsAlive || _host.Parent != desktop)
        {
            DisconnectFromDesktop();
            _host = new DesktopPanelHost(desktop, this, "WinDesk · " + (_stack?.Name ?? _models[0].Name));
        }
        ClampPosition(); UpdateSize(); UpdateBounds();
        _host.SetVisible(_requestedVisible);
    }

    internal void DisconnectFromDesktop()
    {
        if (Controller.IsShortcutDragging) Controller.EndShortcutDrag(default, false);
        _openTimer.Stop(); _closeTimer.Stop(); _sectionScrollTimer.Stop();
        _dragPreview?.Dispose(); _dragPreview = null;
        if (_dragging) { _dragging = false; _draggedSectionId = null; Controller.CancelPanelDrag(); }
        foreach (var menu in _menus.ToArray()) menu.IsOpen = false;
        _host?.Dispose(); _host = null;
    }

    public void Show() { _requestedVisible = true; _host?.SetVisible(true); }
    public void Hide() { _requestedVisible = false; Collapse(); _host?.SetVisible(false); }

    internal void HoverSection(Guid id)
    {
        if (_closed || !Contains(id) || _dragging || _anotherPanelDragging) return;
        _closeTimer.Stop(); _openTimer.Stop(); _pendingSection = id;
        if (ActiveSectionId == id) return;
        QueueHover();
    }

    private bool IsWaitingToCloseSection => _pendingSection.HasValue && ActiveSectionId is Guid current &&
        current != _pendingSection && _models.Any(model => model.Id == current && !model.IsPinned);

    private void QueueHover()
    {
        // Switching closes the current body, so honor the same delay as leaving
        // the area. Opening from a closed/pinned group has nothing to close.
        _openTimer.Stop();
        _openTimer.Interval = TimeSpan.FromMilliseconds(IsWaitingToCloseSection ? Controller.Workspace.CloseDelayMs : 140);
        _openTimer.Start();
    }

    public void Expand(Guid? sectionId = null)
    {
        if (_closed) return;
        var id = sectionId ?? _models[0].Id;
        if (!Contains(id)) return;
        _openTimer.Stop(); _pendingSection = null; _closeTimer.Stop(); ActiveSectionId = id;
        ApplyExpandedState();
        if (!IsMouseOver) QueueCollapse();
    }

    public void Collapse()
    {
        _openTimer.Stop(); _pendingSection = null; ActiveSectionId = null;
        ApplyExpandedState();
    }

    private void ApplyExpandedState(bool animate = true)
    {
        foreach (var section in _sections)
            section.SetExpanded(section.Model.IsPinned || section.Model.Id == ActiveSectionId || section.Model.Id == _shortcutSourceSectionId, animate);
        double targetHeight = 2 + _sections.Sum(s => s.Model.IsPinned || s.Model.Id == ActiveSectionId || s.Model.Id == _shortcutSourceSectionId ? s.OpenHeight : 44);
        ClampPosition(targetHeight);
    }

    internal void HoldShortcutSource(Guid? id)
    {
        _shortcutSourceSectionId = id;
        ApplyExpandedState();
    }

    internal void PinChanged(Guid id)
    {
        var section = _sections.Single(s => s.Model.Id == id);
        section.RefreshPin();
        if (!section.Model.IsPinned && ActiveSectionId == id) ActiveSectionId = null;
        ApplyExpandedState();
        if (!IsMouseOver) QueueCollapse();
    }

    internal void UpdateSize()
    {
        if (_closed || _sections.Count == 0) return;
        double fullHeight = 2 + _sections.Sum(s => s.Height);
        double maximumHeight = MaximumHeight;
        _viewport.VerticalScrollBarVisibility = fullHeight > maximumHeight
            ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        Height = Math.Min(fullHeight, maximumHeight);
        UpdateBounds();
    }

    private double MaximumHeight
    {
        get
        {
            double scale = _host?.Scale ?? 1;
            var work = WorkingAreaAt(new System.Drawing.Point((int)(ScreenX * scale), (int)(ScreenY * scale)));
            return Math.Max(46, work.Height / scale - 8);
        }
    }

    internal void Refresh()
    {
        foreach (var section in _sections) section.Refresh();
        ApplyExpandedState();
        if (Handle != IntPtr.Zero) DesktopNative.SetWindowText(Handle, "WinDesk · " + (_stack?.Name ?? _models[0].Name));
        UpdateSize();
    }

    private void QueueCollapse()
    {
        _closeTimer.Stop();
        _closeTimer.Interval = TimeSpan.FromMilliseconds(Controller.Workspace.CloseDelayMs);
        _closeTimer.Start();
    }

    internal void RefreshCloseDelay()
    {
        // A change also applies to a countdown already waiting to close.
        bool waiting = _closeTimer.IsEnabled;
        _closeTimer.Stop();
        _closeTimer.Interval = TimeSpan.FromMilliseconds(Controller.Workspace.CloseDelayMs);
        if (waiting) _closeTimer.Start();
        if (_openTimer.IsEnabled && IsWaitingToCloseSection) QueueHover();
    }

    internal void BeginDrag()
    {
        if (_host is null || !DesktopNative.GetCursorPos(out _dragStart)) return;
        BeginDrag(_dragStart);
    }
    internal void BeginDrag(DesktopNative.Point point)
    {
        if (_host is null || !_host.IsAlive) return;
        _dragStart = point;
        _draggedSectionId = null;
        _dragging = true; _dragMoved = false; _closeTimer.Stop(); _openTimer.Stop();
        _dragX = ScreenX; _dragY = ScreenY;
        Controller.StartPanelDrag(this);
    }
    internal void BeginSectionDrag(Guid id)
    {
        if (DesktopNative.GetCursorPos(out var point)) BeginSectionDrag(id, point);
    }
    internal void BeginSectionDrag(Guid id, DesktopNative.Point point)
    {
        if (!Contains(id) || _host is null) return;
        BeginDrag(point);
        if (_stack is null) return;
        _draggedSectionId = id;
        // Finish any hover animation before measuring stable header positions.
        ApplyExpandedState(animate: false); UpdateLayout();
        DesktopNative.GetWindowRect(Handle, out _originalBounds);
        var origin = _sections.Single(s => s.Model.Id == id).HeaderScreenOrigin;
        _grabX = (point.X - origin.X) / _host.Scale;
        _grabY = (point.Y - origin.Y) / _host.Scale;
    }
    internal bool IsOutsideOriginalArea(DesktopNative.Point point) =>
        point.X < _originalBounds.Left - 12 || point.X >= _originalBounds.Right + 12 ||
        point.Y < _originalBounds.Top - 12 || point.Y >= _originalBounds.Bottom + 12;
    internal void ContinueDrag()
    {
        if (!_dragging || _host is null || !DesktopNative.GetCursorPos(out var point)) return;
        ContinueDrag(point);
    }
    internal void ContinueDrag(DesktopNative.Point point)
    {
        if (!_dragging || _host is null || !_host.IsAlive) return;
        if (!_dragMoved && Math.Abs(point.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance * _host.Scale &&
            Math.Abs(point.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance * _host.Scale) return;
        _dragMoved = true;
        if (_draggedSectionId is Guid id)
        {
            _sectionScrollTimer.Start();
            _detachX = point.X / _host.Scale - _grabX;
            _detachY = point.Y / _host.Scale - _grabY;
            if (_dragPreview is null)
            {
                try { CreateDragPreview(id); }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                { _dragPreview?.Dispose(); _dragPreview = null; System.Diagnostics.Trace.WriteLine($"WinDesk drag preview: {ex}"); }
            }
            _dragPreview?.SetBounds(_detachX, _detachY, Width, 62);
            _dragPreview?.SetVisible(true);
        }
        else MoveTo(_dragX + (point.X - _dragStart.X) / _host.Scale, _dragY + (point.Y - _dragStart.Y) / _host.Scale);
        Controller.UpdatePanelDrag(this, point);
    }
    private void CreateDragPreview(Guid id)
    {
        if (_host is null) return;
        var model = _models.Single(p => p.Id == id);
        var text = new StackPanel { Margin = new Thickness(14, 9, 12, 7) };
        text.Children.Add(new TextBlock { Text = model.Name, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis });
        _dragPreviewHint = new TextBlock { Text = "Grubu dışarı taşı", FontSize = 11, Foreground = ManagerWindow.Brush("#93E7D5"), Margin = new Thickness(0, 4, 0, 0) };
        text.Children.Add(_dragPreviewHint);
        var content = new Border { Width = Width, Height = 62, Child = text, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), BorderBrush = ManagerWindow.Brush(model.Color), Background = ManagerWindow.Brush("#1A3030") };
        _dragPreview = new DesktopPanelHost(_host.Parent, content, "WinDesk · Grubu taşı");
    }
    internal int? SectionInsertionIndex(DesktopNative.Point point)
    {
        if (!_draggedSectionId.HasValue || _stack is null || !IsDropAvailable ||
            !DesktopNative.GetWindowRect(Handle, out var rect) ||
            point.X < rect.Left || point.X >= rect.Right || point.Y < rect.Top || point.Y >= rect.Bottom) return null;
        var local = PointFromScreen(new Point(point.X, point.Y));
        for (int i = 0; i < _sections.Count; i++)
        {
            double top = _sections[i].TranslatePoint(new Point(), this).Y;
            if (local.Y < top + 22) return i;
        }
        return _sections.Count;
    }

    internal void ScrollSectionsForDrop(DesktopNative.Point point)
    {
        if (_viewport.ScrollableHeight <= 0 || SectionInsertionIndex(point) is null) return;
        var local = _viewport.PointFromScreen(new Point(point.X, point.Y));
        if (local.Y < 22) _viewport.ScrollToVerticalOffset(_viewport.VerticalOffset - 14);
        else if (local.Y > _viewport.ActualHeight - 22) _viewport.ScrollToVerticalOffset(_viewport.VerticalOffset + 14);
        _viewport.UpdateLayout();
    }

    private void ShowSectionDrop(int? index)
    {
        SectionDropIndex = index;
        _sectionDropLine.Visibility = _sectionDropLabel.Visibility = index.HasValue ? Visibility.Visible : Visibility.Collapsed;
        if (index is not int slot) return;
        var anchor = _sections[Math.Min(slot, _sections.Count - 1)];
        double y = anchor.TranslatePoint(new Point(), this).Y + (slot == _sections.Count ? anchor.ActualHeight : 0);
        y = Math.Clamp(y, 3, Math.Max(3, ActualHeight - 3));
        _sectionDropLine.Width = Math.Max(0, ActualWidth - 24);
        Canvas.SetLeft(_sectionDropLine, 12); Canvas.SetTop(_sectionDropLine, y - 1.5);
        Canvas.SetLeft(_sectionDropLabel, Math.Max(12, ActualWidth - 96));
        Canvas.SetTop(_sectionDropLabel, y < 28 ? y + 4 : y - 25);
    }

    internal void ApplySectionOrder(IEnumerable<PanelModel> ordered)
    {
        var models = ordered.ToList();
        var sections = models.Select(m => _sections.Single(s => s.Model.Id == m.Id)).ToList();
        double offset = _viewport.VerticalOffset;
        _models.Clear(); _models.AddRange(models);
        _sections.Clear(); _sections.AddRange(sections);
        _sectionsLayout.Children.Clear();
        foreach (var section in _sections) _sectionsLayout.Children.Add(section);
        ApplyExpandedState(animate: false); UpdateLayout(); _viewport.ScrollToVerticalOffset(offset);
    }

    internal void SetSectionDropHint(bool allowed, bool merging, int? reorderIndex = null)
    {
        ShowSectionDrop(reorderIndex);
        // While sorting, leave all headers visible instead of covering them
        // with the detach preview. Outside the area the normal preview returns.
        _dragPreview?.SetVisible(reorderIndex is null);
        if (_dragPreviewHint is not null)
            _dragPreviewHint.Text = allowed ? merging ? "Birleştirmek için bırak" : "Ayrı panel olarak bırak" : "Ayırmak için alanın dışına taşı";
    }
    internal void EndDrag(bool commit = true)
    {
        if (!DesktopNative.GetCursorPos(out var point)) commit = false;
        EndDrag(point, commit);
    }
    internal void EndDrag(DesktopNative.Point point, bool commit = true)
    {
        if (!_dragging) return;
        _dragging = false; _positionTimer.Stop(); _sectionScrollTimer.Stop();
        bool sectionDrag = _draggedSectionId.HasValue;
        bool dropped = commit && _dragMoved && Controller.CompletePanelDrag(this, point);
        _dragPreview?.Dispose(); _dragPreview = null; _dragPreviewHint = null; _draggedSectionId = null;
        Controller.CancelPanelDrag();
        if (dropped) return;
        if (!sectionDrag) { ClampPosition(); UpdateBounds(); Controller.Save(); }
        if (!IsMouseOver) QueueCollapse();
    }
    internal void SetDragState(bool active, bool target, bool sourceHint)
    {
        _anotherPanelDragging = active;
        if (!active) ShowSectionDrop(null);
        if (active) { _openTimer.Stop(); _closeTimer.Stop(); }
        _frame.BorderBrush = ManagerWindow.Brush(target ? "#73D9C3" : "#465568");
        _dropHint.Visibility = target || (sourceHint && !_draggedSectionId.HasValue) ? Visibility.Visible : Visibility.Collapsed;
        ((TextBlock)_dropHint.Child).Text = target ? "Buraya bırakınca birleşir" : "Birleştirmek için bırak";
        _frame.Background = ManagerWindow.Brush(target ? "#253D3D" : "#F21A2230");
        if (!active && !IsMouseOver && !_closed) QueueCollapse();
    }
    internal void MoveTo(double x, double y)
    {
        SavedX = x; SavedY = y; ClampPosition();
        // Only an explicit move changes the preferred location. A temporary
        // monitor/Explorer fallback must not replace it during startup or save.
        SavedX = ScreenX; SavedY = ScreenY;
        UpdateBounds();
        _positionTimer.Stop(); _positionTimer.Start();
    }

    private void ClampPosition(double? targetHeight = null)
    {
        _screenX = SavedX; _screenY = SavedY;
        if (_host is null || !_host.IsAlive) return;
        double scale = _host.Scale;
        var work = WorkingAreaAt(new System.Drawing.Point((int)(ScreenX * scale), (int)(ScreenY * scale)));
        if (!DesktopNative.GetClientRect(_host.Parent, out var rect)) return;
        var origin = new DesktopNative.Point(0, 0);
        if (!DesktopNative.ClientToScreen(_host.Parent, ref origin)) return;
        double left = Math.Max(work.Left, origin.X) / scale, top = Math.Max(work.Top, origin.Y) / scale;
        double right = Math.Min(work.Right, origin.X + rect.Right) / scale, bottom = Math.Min(work.Bottom, origin.Y + rect.Bottom) / scale;
        _screenX = Math.Clamp(ScreenX, left, Math.Max(left, right - Width));
        _screenY = Math.Clamp(ScreenY, top, Math.Max(top, bottom - Math.Min(targetHeight ?? Height, MaximumHeight)));
    }

    private void UpdateBounds() => _host?.SetBounds(ScreenX, ScreenY, Width, Height);
    public void CapturePosition() { /* Dragging updates the shared model immediately, including before exit. */ }

    internal void TrackMenu(ContextMenu menu)
    {
        _menus.Add(menu);
        menu.Opened += (_, _) => { _openMenus++; _closeTimer.Stop(); _openTimer.Stop(); _pendingSection = null; };
        menu.Closed += (_, _) => { _openMenus = Math.Max(0, _openMenus - 1); if (!_closed) QueueCollapse(); };
    }
    internal void UntrackMenu(ContextMenu menu) { menu.IsOpen = false; _menus.Remove(menu); }
    internal void WithDialog(Action action)
    { _dialogOpen = true; _openTimer.Stop(); _pendingSection = null; try { action(); } finally { _dialogOpen = false; if (!_closed) QueueCollapse(); } }

    public void Close() => Dispose();
    public void Dispose()
    {
        if (_closed) return;
        _closed = true; _openTimer.Stop(); _closeTimer.Stop(); _positionTimer.Stop();
        DisconnectFromDesktop();
    }
}
