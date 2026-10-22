using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WinDesk.Core;

public sealed class Workspace
{
    public int Version { get; set; } = 4;
    public int CloseDelayMs { get; set; } = 550;
    public List<PanelModel> Panels { get; set; } = new();
    public List<PanelStack> Stacks { get; set; } = new();

    public static Workspace CreateDefault() => new()
    {
        Panels = new()
        {
            new() { Name = "Günlük", X = 100, Y = 90, Color = "#73D9C3" },
            new() { Name = "İş ve üretim", X = 100, Y = 370, Color = "#AFA4F4" }
        }
    };

    public void Validate()
    {
        if (Version is not (1 or 2 or 3 or 4) || Panels is null || Panels.Count > 100 || Stacks is null)
            throw new InvalidDataException("Düzen dosyasının sürümü veya içeriği desteklenmiyor.");
        CloseDelayMs = Math.Clamp(CloseDelayMs, 200, 2000);
        var ids = new HashSet<Guid>();
        foreach (var panel in Panels)
        {
            if (panel is null || panel.Id == Guid.Empty || !ids.Add(panel.Id) ||
                string.IsNullOrWhiteSpace(panel.Name) || panel.Name.Length > 60 ||
                panel.Items is null || panel.Items.Count > 2000)
                throw new InvalidDataException("Panel bilgileri geçersiz.");
            if (!double.IsFinite(panel.X) || !double.IsFinite(panel.Y))
                throw new InvalidDataException("Panel konumu geçersiz.");
            panel.Color = PanelModel.NormalizeColor(panel.Color) ?? PanelModel.Colors[0];
            if (panel.Items.Any(item => item is null || string.IsNullOrWhiteSpace(item.Path) ||
                !Path.IsPathFullyQualified(item.Path) || string.IsNullOrWhiteSpace(item.Name) ||
                item.Arguments is null || item.WorkingDirectory is null || item.OwnedShortcutPath is null ||
                (item.OwnedShortcutPath.Length > 0 && !Path.IsPathFullyQualified(item.OwnedShortcutPath))))
                throw new InvalidDataException("Kısayol bilgileri geçersiz.");
        }
        var stackIds = new HashSet<Guid>();
        foreach (var stack in Stacks)
        {
            if (stack is null || stack.Id == Guid.Empty || ids.Contains(stack.Id) || !stackIds.Add(stack.Id) ||
                string.IsNullOrWhiteSpace(stack.Name) || stack.Name.Length > 60 ||
                !double.IsFinite(stack.X) || !double.IsFinite(stack.Y) || Panels.Count(p => p.StackId == stack.Id) < 2)
                throw new InvalidDataException("Birleşik alan bilgileri geçersiz.");
        }
        if (Panels.Any(p => p.StackId.HasValue && !stackIds.Contains(p.StackId.Value)))
            throw new InvalidDataException("Grubun bağlı olduğu alan bulunamadı.");
        Version = 4; // Keep older layouts; launch metadata is optional until links are imported.
    }

    public PanelStack Combine(IEnumerable<Guid> panelIds, string name)
    {
        var selected = panelIds.Distinct().ToList();
        if (selected.Count < 2 || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 60)
            throw new ArgumentException("En az iki grup seçin ve alana bir ad verin.");
        var members = selected.Select(id => Panels.SingleOrDefault(p => p.Id == id)
            ?? throw new ArgumentException("Seçilen grup bulunamadı.")).ToList();
        if (members.Any(p => p.StackId.HasValue))
            throw new ArgumentException("Önce mevcut birleşik alanı ayırın.");
        var stack = new PanelStack { Name = name.Trim(), X = members[0].X, Y = members[0].Y };
        foreach (var panel in members) panel.StackId = stack.Id;
        Stacks.Add(stack); return stack;
    }

    public void Separate(Guid stackId)
    {
        var stack = Stacks.Single(s => s.Id == stackId);
        int index = 0;
        foreach (var panel in Panels.Where(p => p.StackId == stackId))
        { panel.StackId = null; panel.X = stack.X; panel.Y = stack.Y + index++ * 64; }
        Stacks.Remove(stack);
    }

    /// <summary>Drop a standalone group or whole accordion onto another area.</summary>
    public PanelStack MergeAreas(Guid sourceId, Guid targetId)
    {
        List<PanelModel> Members(Guid id)
        {
            if (Stacks.Any(s => s.Id == id)) return Panels.Where(p => p.StackId == id).ToList();
            var panel = Panels.SingleOrDefault(p => p.Id == id && p.StackId is null);
            return panel is null ? throw new ArgumentException("Birleştirilecek alan bulunamadı.") : new() { panel };
        }
        if (sourceId == targetId) throw new ArgumentException("Bir alan kendi üzerine bırakılamaz.");
        var source = Members(sourceId);
        var target = Members(targetId);
        var targetStack = Stacks.SingleOrDefault(s => s.Id == targetId);
        var result = targetStack ?? new PanelStack { X = target[0].X, Y = target[0].Y };
        // Validate the entire selection before mutating membership. Keep the target
        // first, append the dragged sections, and preserve every shortcut object.
        int insertionIndex = Panels.FindIndex(p => target.Contains(p) || source.Contains(p));
        var ordered = target.Concat(source).ToList();
        foreach (var member in ordered) member.StackId = result.Id;
        Panels.RemoveAll(p => ordered.Contains(p));
        Panels.InsertRange(insertionIndex, ordered);
        Stacks.RemoveAll(s => s.Id == sourceId);
        if (targetStack is null) Stacks.Add(result);
        return result;
    }

    /// <summary>Move a member to a gap in its accordion without moving other areas.</summary>
    public bool ReorderPanel(Guid panelId, int insertionIndex)
    {
        var panel = Panels.SingleOrDefault(p => p.Id == panelId);
        if (panel?.StackId is not Guid stackId || !Stacks.Any(s => s.Id == stackId))
            throw new ArgumentException("Sıralanacak grup bir birleşik alanda değil.");
        var slots = Enumerable.Range(0, Panels.Count).Where(i => Panels[i].StackId == stackId).ToArray();
        if (insertionIndex < 0 || insertionIndex > slots.Length)
            throw new ArgumentException("Grubun bırakılacağı sıra geçersiz.");
        var members = slots.Select(i => Panels[i]).ToList();
        int oldIndex = members.IndexOf(panel);
        if (oldIndex < insertionIndex) insertionIndex--;
        if (oldIndex == insertionIndex) return false;
        members.RemoveAt(oldIndex); members.Insert(insertionIndex, panel);
        // Reuse the existing member slots; unrelated panels retain their order.
        for (int i = 0; i < slots.Length; i++) Panels[slots[i]] = members[i];
        return true;
    }

    public void RemovePanel(Guid panelId)
    {
        var panel = Panels.Single(p => p.Id == panelId);
        Panels.Remove(panel);
        if (panel.StackId is Guid stackId && Panels.Count(p => p.StackId == stackId) < 2) Separate(stackId);
    }

    /// <summary>Move an existing shortcut to a gap in the destination list.</summary>
    public void MoveShortcut(Guid sourceId, ShortcutModel shortcut, Guid targetId, int insertionIndex)
    {
        var source = Panels.SingleOrDefault(p => p.Id == sourceId);
        var target = Panels.SingleOrDefault(p => p.Id == targetId);
        if (source is null || target is null || !source.Items.Contains(shortcut) ||
            insertionIndex < 0 || insertionIndex > target.Items.Count)
            throw new ArgumentException("Taşınacak kısayol veya bırakma yeri bulunamadı.");
        int oldIndex = source.Items.IndexOf(shortcut);
        if (source == target && oldIndex < insertionIndex) insertionIndex--;
        var duplicate = source != target ? target.Items.FirstOrDefault(i =>
            i.SameLaunchTarget(shortcut)) : null;
        if (source != target && duplicate is null && target.Items.Count >= 2000)
            throw new ArgumentException("Hedef grup en fazla 2000 kısayol içerebilir.");
        if (duplicate is not null)
        {
            int duplicateIndex = target.Items.IndexOf(duplicate);
            target.Items.Remove(duplicate);
            if (duplicateIndex < insertionIndex) insertionIndex--;
        }
        source.Items.Remove(shortcut);
        target.Items.Insert(insertionIndex, shortcut);
    }

    public void DetachPanel(Guid panelId, double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentException("Panel konumu geçersiz.");
        var panel = Panels.SingleOrDefault(p => p.Id == panelId)
            ?? throw new ArgumentException("Ayrılacak grup bulunamadı.");
        if (panel.StackId is not Guid stackId || !Stacks.Any(s => s.Id == stackId))
            throw new ArgumentException("Bu grup bir birleşik alanda değil.");
        panel.StackId = null; panel.X = x; panel.Y = y;
        // Keep the original area in place. A single remaining member becomes a
        // standalone panel automatically, retaining its contents and pin state.
        if (Panels.Count(p => p.StackId == stackId) < 2) Separate(stackId);
    }
}

public sealed class PanelStack
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Birleşik alan";
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class PanelModel
{
    public static readonly string[] Colors = { "#73D9C3", "#AFA4F4", "#EFC17A", "#83BDF5", "#EF9DB6" };
    public static string? NormalizeColor(string? value)
    {
        string color = value?.Trim() ?? "";
        if (color.StartsWith('#')) color = color[1..];
        return color.Length == 6 && color.All(Uri.IsHexDigit) ? "#" + color.ToUpperInvariant() : null;
    }
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Yeni panel";
    public Guid? StackId { get; set; }
    public bool IsPinned { get; set; }
    public string Color { get; set; } = Colors[0];
    public double X { get; set; } = 100;
    public double Y { get; set; } = 90;
    public List<ShortcutModel> Items { get; set; } = new();

    public bool AddShortcut(ShortcutModel shortcut)
    {
        if (Items.Any(item => item.SameLaunchTarget(shortcut))) return false;
        if (Items.Count >= 2000) throw new ArgumentException("Bir grup en fazla 2000 kısayol içerebilir.");
        Items.Add(shortcut); return true;
    }

    public int AddPaths(IEnumerable<string> paths)
    {
        int added = 0;
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) continue;
            var full = Path.GetFullPath(path);
            if ((!File.Exists(full) && !Directory.Exists(full)) ||
                Items.Any(item => string.Equals(item.Path, full, StringComparison.OrdinalIgnoreCase))) continue;
            var name = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(name)) name = full;
            if (Path.GetExtension(name).ToLowerInvariant() is ".lnk" or ".url" or ".exe") name = Path.GetFileNameWithoutExtension(name);
            Items.Add(new() { Name = name, Path = full });
            added++;
        }
        return added;
    }
}

public sealed class ShortcutModel
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public string OwnedShortcutPath { get; set; } = "";
    public int WindowStyle { get; set; } = 1;

    public bool SameLaunchTarget(ShortcutModel other) =>
        string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Arguments, other.Arguments, StringComparison.Ordinal) &&
        string.Equals(WorkingDirectory, other.WorkingDirectory, StringComparison.OrdinalIgnoreCase);
}

public sealed class WorkspaceStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string FilePath { get; }
    public WorkspaceStore(string filePath) => FilePath = Path.GetFullPath(filePath);

    public Workspace Load()
    {
        string json;
        try { json = File.ReadAllText(FilePath); }
        catch (FileNotFoundException) { return Workspace.CreateDefault(); }
        catch (DirectoryNotFoundException) { return Workspace.CreateDefault(); }
        // Access and sharing errors must not silently look like an empty first run.
        var state = JsonSerializer.Deserialize<Workspace>(json, Options)
            ?? throw new InvalidDataException("Düzen dosyası boş.");
        state.Validate();
        return state;
    }

    public void Save(Workspace workspace)
    {
        workspace.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, workspace, Options);
            stream.Flush(flushToDisk: true);
        }
        // Replace is atomic on the local Windows filesystem; retain the last good revision.
        if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak");
        else File.Move(temporary, FilePath);
    }
}
