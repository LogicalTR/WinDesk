using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using WinDesk.Core;

var directory = Path.Combine(Path.GetTempPath(), "WinDesk.Tests." + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
int assertions = 0;
void Check(bool condition, string message)
{ if (!condition) throw new Exception(message); assertions++; Console.WriteLine("PASS " + message); }
try
{
    var file = Path.Combine(directory, "workspace.json");
    var store = new WorkspaceStore(file);
    var state = store.Load();
    Check(state.Panels.Count == 2 && !File.Exists(file), "First launch creates a model without touching disk");
    var target = Path.Combine(directory, "example.txt");
    File.WriteAllText(target, "Original user file");
    var panel = state.Panels[0];
    Check(panel.AddPaths(new[] { target, target.ToUpperInvariant(), "relative.txt", Path.Combine(directory, "missing.exe") }) == 1,
        "Drop filters duplicate, relative, and missing targets");
    Check(File.ReadAllText(target) == "Original user file", "Adding a shortcut leaves its source intact");
    panel.X = -800; panel.Y = 180; panel.Name = "Çalışma & üretim"; state.CloseDelayMs = 850;
    store.Save(state);
    var restored = store.Load();
    Check(restored.Panels[0].Name == panel.Name && restored.Panels[0].X == -800 && restored.CloseDelayMs == 850,
        "Unicode names, negative monitor coordinates, and preferences survive restart");
    Check(restored.Panels[0].Items[0].Path == target, "Shortcut target survives restart");
    restored.Panels[0].Items.Clear(); store.Save(restored);
    Check(File.Exists(target), "Removing a shortcut never deletes its target");
    var previous = new WorkspaceStore(file + ".bak").Load();
    Check(previous.Panels[0].Items.Count == 1, "Atomic save keeps the previous valid layout as backup");
    Check(!File.Exists(file + ".tmp"), "Atomic save leaves no temporary file after success");
    File.WriteAllText(file, "{broken JSON");
    bool rejected = false;
    try { store.Load(); } catch (JsonException) { rejected = true; }
    Check(rejected && File.ReadAllText(file) == "{broken JSON", "Corrupt configuration is rejected and preserved");
    var unsupported = Workspace.CreateDefault(); unsupported.Version = 99;
    rejected = false; try { unsupported.Validate(); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Future configuration versions cannot be silently overwritten");
    var duplicate = Workspace.CreateDefault(); duplicate.Panels[1].Id = duplicate.Panels[0].Id;
    rejected = false; try { duplicate.Validate(); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Duplicate panel identifiers are rejected before window creation");
    var bad = Workspace.CreateDefault(); bad.CloseDelayMs = -1; bad.Panels[0].Color = "invalid"; bad.Validate();
    Check(bad.CloseDelayMs == 200 && bad.Panels[0].Color == PanelModel.Colors[0], "Out-of-range preferences recover safely");
    var unreadablePath = Path.Combine(directory, "not-a-file.json"); Directory.CreateDirectory(unreadablePath);
    rejected = false; try { new WorkspaceStore(unreadablePath).Load(); } catch (UnauthorizedAccessException) { rejected = true; }
    Check(rejected, "An inaccessible workspace path cannot silently create an empty replacement layout");
    var customColors = Workspace.CreateDefault();
    customColors.Panels[0].Color = "#12abef"; customColors.Panels[1].Color = "#000000";
    store.Save(customColors); var customReload = store.Load();
    Check(customReload.Panels[0].Color == "#12ABEF" && customReload.Panels[1].Color == "#000000", "Custom RGB colors survive save and restart without reverting to presets");
    Check(PanelModel.NormalizeColor(" FfFfFf ") == "#FFFFFF" && PanelModel.NormalizeColor("#GG1122") is null && PanelModel.NormalizeColor("#00FFFFFF") is null,
        "Hex input accepts all opaque RGB colors and rejects malformed or transparent values");
    var legacy = Workspace.CreateDefault(); legacy.Version = 1; legacy.Validate();
    Check(legacy.Version == 4 && legacy.Stacks.Count == 0 && legacy.Panels.TrueForAll(p => !p.IsPinned), "Version 1 upgrades without altering existing groups");
    var grouped = Workspace.CreateDefault();
    grouped.Panels[0].AddPaths(new[] { target });
    var firstId = grouped.Panels[0].Id; var secondId = grouped.Panels[1].Id;
    var stack = grouped.Combine(new[] { firstId, secondId }, "Ortak alan");
    Check(grouped.Stacks.Count == 1 && grouped.Panels.TrueForAll(p => p.StackId == stack.Id), "Combining groups creates one shared area");
    Check(grouped.Panels[0].Items.Count == 1 && File.Exists(target), "Combining preserves group contents and original files");
    store.Save(grouped); var groupedReload = store.Load();
    Check(groupedReload.Stacks[0].Name == "Ortak alan" && groupedReload.Panels[1].StackId == stack.Id, "Combined membership survives restart");
    rejected = false; try { grouped.Combine(new[] { firstId, secondId }, "Nested"); } catch (ArgumentException) { rejected = true; }
    Check(rejected && grouped.Stacks.Count == 1, "Already grouped panels cannot silently change containers");
    stack.X = 420; stack.Y = 120; grouped.Separate(stack.Id);
    Check(grouped.Stacks.Count == 0 && grouped.Panels.TrueForAll(p => p.StackId is null) && grouped.Panels[0].X == 420 && grouped.Panels[1].Y == 184,
        "Separating preserves groups and positions them near their former shared area");
    stack = grouped.Combine(new[] { firstId, secondId }, "Again"); grouped.RemovePanel(secondId);
    Check(grouped.Stacks.Count == 0 && grouped.Panels[0].StackId is null && grouped.Panels[0].Items.Count == 1,
        "Removing one of two members safely dissolves the container");
    var orphan = Workspace.CreateDefault(); orphan.Panels[0].StackId = Guid.NewGuid();
    rejected = false; try { orphan.Validate(); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "Orphaned group membership is rejected before rendering");
    var single = Workspace.CreateDefault();
    rejected = false; try { single.Combine(new[] { single.Panels[0].Id, single.Panels[0].Id }, "Single"); } catch (ArgumentException) { rejected = true; }
    Check(rejected && single.Stacks.Count == 0 && single.Panels.TrueForAll(p => p.StackId is null), "Invalid selection cannot partially mutate the workspace");
    var dropped = Workspace.CreateDefault();
    var dragged = dropped.Panels[0]; var destination = dropped.Panels[1];
    dragged.AddPaths(new[] { target });
    destination.X = 640; destination.Y = 220;
    var droppedStack = dropped.MergeAreas(dragged.Id, destination.Id);
    Check(droppedStack.X == 640 && droppedStack.Y == 220 && dropped.Panels[0] == destination && dropped.Panels[1] == dragged,
        "Dropping keeps the destination position and appends the dragged group after it");
    Check(dragged.Items.Count == 1 && File.ReadAllText(target) == "Original user file", "Drag merge preserves shortcuts and source files");
    var extra = new PanelModel { Name = "Third" }; dropped.Panels.Add(extra);
    dropped.MergeAreas(extra.Id, droppedStack.Id);
    Check(dropped.Stacks.Count == 1 && droppedStack.Id == extra.StackId && dropped.Panels[2] == extra,
        "Dropping onto an existing accordion adds the group without replacing its identity");
    var a = new PanelModel { Name = "A" }; var b = new PanelModel { Name = "B" };
    dropped.Panels.Add(a); dropped.Panels.Add(b);
    var otherStack = dropped.Combine(new[] { a.Id, b.Id }, "Other");
    dropped.MergeAreas(otherStack.Id, droppedStack.Id); dropped.Validate();
    Check(dropped.Stacks.Count == 1 && dropped.Panels.Count == 5 && dropped.Panels.TrueForAll(p => p.StackId == droppedStack.Id),
        "Two accordions merge without orphaning or losing any group");
    rejected = false; try { dropped.MergeAreas(droppedStack.Id, droppedStack.Id); } catch (ArgumentException) { rejected = true; }
    Check(rejected && dropped.Panels.Count == 5 && dropped.Stacks.Count == 1, "Dropping an accordion onto itself leaves the workspace unchanged");
    rejected = false; try { dropped.MergeAreas(droppedStack.Id, Guid.NewGuid()); } catch (ArgumentException) { rejected = true; }
    Check(rejected && dropped.Panels.TrueForAll(p => p.StackId == droppedStack.Id), "Stale drop destinations cannot partially change membership");
    store.Save(dropped); var afterDrop = store.Load();
    Check(afterDrop.Stacks.Count == 1 && afterDrop.Panels.Count == 5 && afterDrop.Panels[1].Items.Count == 1,
        "Merged areas and shortcut contents survive a save and reload");
    dropped.Separate(droppedStack.Id);
    Check(dropped.Stacks.Count == 0 && dropped.Panels.Count == 5 && dropped.Panels.TrueForAll(p => p.StackId is null),
        "Drag-merged groups can all be separated again");
    var detachable = Workspace.CreateDefault();
    var pinned = detachable.Panels[0]; pinned.IsPinned = true; pinned.AddPaths(new[] { target });
    var remaining = detachable.Panels[1];
    var third = new PanelModel { Name = "Third" }; detachable.Panels.Add(third);
    var detachStack = detachable.Combine(new[] { pinned.Id, remaining.Id, third.Id }, "Detach");
    detachStack.X = 360; detachStack.Y = 180;
    detachable.DetachPanel(pinned.Id, 70, 600);
    Check(pinned.StackId is null && pinned.X == 70 && pinned.Y == 600 && pinned.IsPinned && pinned.Items.Count == 1,
        "Detaching one section preserves its pin, contents and requested destination");
    Check(detachable.Stacks.Count == 1 && detachStack.X == 360 && remaining.StackId == detachStack.Id && third.StackId == detachStack.Id,
        "Detaching from three groups keeps the other two in their original area");
    detachable.DetachPanel(third.Id, 900, 400); detachable.Validate();
    Check(detachable.Stacks.Count == 0 && remaining.StackId is null && remaining.X == 360 && remaining.Y == 180,
        "The last remaining group becomes independent at the old area position");
    store.Save(detachable); var pinnedReload = store.Load();
    Check(pinnedReload.Version == 4 && pinnedReload.Panels[0].IsPinned && pinnedReload.Panels[0].StackId is null && pinnedReload.Panels[0].Items.Count == 1,
        "Pin preference and detached membership survive a save and restart");
    rejected = false; try { detachable.DetachPanel(pinned.Id, 1, 2); } catch (ArgumentException) { rejected = true; }
    Check(rejected && pinned.X == 70 && pinned.Y == 600, "Detaching an already independent group cannot silently move it");
    var badDetach = detachable.Combine(new[] { pinned.Id, remaining.Id }, "Invalid destination");
    rejected = false; try { detachable.DetachPanel(pinned.Id, double.NaN, 0); } catch (ArgumentException) { rejected = true; }
    Check(rejected && pinned.StackId == badDetach.Id, "Invalid detach coordinates cannot partly remove membership");
    pinned.IsPinned = false; store.Save(detachable);
    Check(!store.Load().Panels[0].IsPinned, "Unpinning is persisted as well as pinning");
    var legacyV2 = Workspace.CreateDefault(); legacyV2.Version = 2;
    legacyV2.Combine(new[] { legacyV2.Panels[0].Id, legacyV2.Panels[1].Id }, "Old area"); legacyV2.Validate();
    Check(legacyV2.Version == 4 && legacyV2.Stacks.Count == 1 && legacyV2.Panels.TrueForAll(p => !p.IsPinned),
        "Version 2 accordions upgrade to unpinned groups without losing membership");
    var moving = Workspace.CreateDefault();
    var sourceItems = moving.Panels[0]; var destinationItems = moving.Panels[1];
    var itemA = new ShortcutModel { Name = "A", Path = target };
    var itemB = new ShortcutModel { Name = "B", Path = Path.Combine(directory, "B.txt") };
    var itemC = new ShortcutModel { Name = "C", Path = Path.Combine(directory, "C.txt") };
    sourceItems.Items.AddRange(new[] { itemA, itemB, itemC });
    moving.MoveShortcut(sourceItems.Id, itemA, sourceItems.Id, 3);
    Check(sourceItems.Items.SequenceEqual(new[] { itemB, itemC, itemA }), "Reordering forward compensates for the removed source index");
    moving.MoveShortcut(sourceItems.Id, itemA, sourceItems.Id, 0);
    Check(sourceItems.Items.SequenceEqual(new[] { itemA, itemB, itemC }), "Reordering backward inserts into the chosen gap");
    moving.MoveShortcut(sourceItems.Id, itemB, sourceItems.Id, 2);
    Check(sourceItems.Items.SequenceEqual(new[] { itemA, itemB, itemC }), "Dropping immediately after itself is a stable no-op");
    moving.MoveShortcut(sourceItems.Id, itemB, destinationItems.Id, 0);
    Check(sourceItems.Items.SequenceEqual(new[] { itemA, itemC }) && destinationItems.Items.Single() == itemB,
        "Moving to another group transfers the same shortcut without duplication");
    destinationItems.Items.Add(new ShortcutModel { Name = "Old duplicate", Path = itemA.Path.ToUpperInvariant() });
    moving.MoveShortcut(sourceItems.Id, itemA, destinationItems.Id, 1);
    Check(sourceItems.Items.Single() == itemC && destinationItems.Items.SequenceEqual(new[] { itemB, itemA }),
        "A matching destination shortcut is consolidated at the requested position");
    rejected = false; try { moving.MoveShortcut(sourceItems.Id, itemC, Guid.NewGuid(), 0); } catch (ArgumentException) { rejected = true; }
    Check(rejected && sourceItems.Items.Single() == itemC, "A stale target cannot remove the source shortcut");
    rejected = false; try { moving.MoveShortcut(sourceItems.Id, itemC, destinationItems.Id, 99); } catch (ArgumentException) { rejected = true; }
    Check(rejected && sourceItems.Items.Single() == itemC, "Invalid insertion positions leave both groups intact");
    store.Save(moving); var movedReload = store.Load();
    Check(movedReload.Panels[1].Items.Select(i => i.Name).SequenceEqual(new[] { "B", "A" }) && File.ReadAllText(target) == "Original user file",
        "Shortcut order and membership persist without changing original files");
    var sorting = Workspace.CreateDefault();
    var sortA = sorting.Panels[0]; var sortB = sorting.Panels[1];
    var sortC = new PanelModel { Name = "Third", IsPinned = true };
    var unrelated = new PanelModel { Name = "Unrelated", X = 999 };
    sortA.AddPaths(new[] { target }); sorting.Panels.Insert(1, unrelated); sorting.Panels.Add(sortC);
    var sortStack = sorting.Combine(new[] { sortA.Id, sortB.Id, sortC.Id }, "Ordered area");
    sortStack.X = 420; sortStack.Y = 160;
    Check(sorting.ReorderPanel(sortA.Id, 3) && sorting.Panels.Where(p => p.StackId == sortStack.Id).SequenceEqual(new[] { sortB, sortC, sortA }),
        "Moving the first group below the last uses the destination gap correctly");
    Check(sorting.Panels[1] == unrelated && sortC.IsPinned && sortA.Items.Count == 1 && sortStack.X == 420 && sortStack.Y == 160,
        "Group sorting preserves other areas, shortcuts, pins and the shared position");
    Check(sorting.ReorderPanel(sortA.Id, 0) && sorting.Panels.Where(p => p.StackId == sortStack.Id).SequenceEqual(new[] { sortA, sortB, sortC }),
        "A group can be moved from the bottom to the top");
    Check(!sorting.ReorderPanel(sortB.Id, 1) && !sorting.ReorderPanel(sortB.Id, 2), "Dropping on either side of the current position is a stable no-op");
    rejected = false; try { sorting.ReorderPanel(sortA.Id, 99); } catch (ArgumentException) { rejected = true; }
    Check(rejected && sorting.Panels[0] == sortA, "Invalid group insertion indices cannot partly change the layout");
    rejected = false; try { sorting.ReorderPanel(unrelated.Id, 0); } catch (ArgumentException) { rejected = true; }
    Check(rejected && sorting.Panels[1] == unrelated, "Sorting requires membership in an existing accordion");
    sorting.ReorderPanel(sortC.Id, 1); store.Save(sorting);
    var sortedReload = store.Load();
    Check(sortedReload.Panels.Where(p => p.StackId == sortStack.Id).Select(p => p.Id).SequenceEqual(new[] { sortA.Id, sortC.Id, sortB.Id }) && sortedReload.Panels.Single(p => p.Id == sortC.Id).IsPinned,
        "The reordered group titles and pin state survive a save and restart");
    Console.WriteLine($"{assertions} assertions passed.");
}
finally
{
    // This unique directory was created by this test; no user-selected paths are deleted.
    Directory.Delete(directory, recursive: true);
}
