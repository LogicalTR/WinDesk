using System;
using System.Linq;
using System.Threading.Tasks;
using WinDesk.App;
using WinDesk.Core;

internal static class HeaderInteractionTests
{
    internal static async Task Run(App app, Action<bool, string> check)
    {
        app.Workspace.Panels.Clear(); app.Workspace.Stacks.Clear();
        var groups = new[] {
            new PanelModel { Name = "Tarayıcılar", X = 150, Y = 100 },
            new PanelModel { Name = "İş ve üretim" },
            new PanelModel { Name = "Günlük" }
        };
        foreach (var group in groups.Take(2))
            for (int i = 0; i < (group == groups[0] ? 6 : 2); i++)
                group.Items.Add(new ShortcutModel { Name = "Öğe " + i, Path = Environment.ProcessPath! });
        app.Workspace.Panels.AddRange(groups);
        app.Workspace.Combine(groups.Select(g => g.Id), "Başlık testi");
        app.SetCloseDelay(1900); app.RebuildPanels();
        var panel = app.DesktopPanels.Single(); panel.Hide();
        foreach (bool topmost in new[] { false, true })
        {
            if (app.PanelsOnTop != topmost) app.TogglePanelLayer();
            panel.Hide(); panel.Expand(groups[0].Id); await Task.Delay(180);
            foreach (var group in new[] { groups[1], groups[2], groups[0] })
            {
                panel.HoverSection(group.Id);
                var origin = panel.Sections.Single(s => s.Model == group).HeaderScreenOrigin;
                var point = new DesktopNative.Point((int)origin.X + 85, (int)origin.Y + 22);
                panel.BeginSectionDrag(group.Id, point); panel.EndHeaderPress(group.Id, point);
                await Task.Delay(180); panel.UpdateLayout();
                check(panel.ActiveSectionId == group.Id && panel.IsSectionExpanded(group.Id) &&
                    groups.Where(g => g != group).All(g => !panel.IsSectionExpanded(g.Id)),
                    $"Clicking {group.Name} opens that group after the first group, topmost={topmost}");
                var sections = panel.Sections.ToArray();
                check(sections.Select((s, index) => index == 0 || Math.Abs(s.HeaderScreenOrigin.Y -
                    (sections[index - 1].HeaderScreenOrigin.Y + sections[index - 1].ActualHeight)) < 2).All(value => value),
                    "Different body heights retain correctly placed, visible group headers");
            }
            var third = panel.Sections.Last().HeaderScreenOrigin;
            var start = new DesktopNative.Point((int)third.X + 85, (int)third.Y + 22);
            panel.BeginSectionDrag(groups[2].Id, start);
            panel.ContinueDrag(new DesktopNative.Point(start.X + 40, start.Y));
            panel.EndHeaderPress(groups[2].Id, start);
            check(panel.ActiveSectionId == groups[0].Id, "Dragging a header does not act as an opening click");
        }
        if (app.PanelsOnTop) app.TogglePanelLayer();
    }
}
