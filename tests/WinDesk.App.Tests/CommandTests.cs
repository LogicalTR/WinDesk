using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WinDesk.App;

internal static class CommandTests
{
    internal static async Task Run(App app, Action<bool, string> check)
    {
        string name = "WinDesk.CommandTest." + Guid.NewGuid().ToString("N");
        var received = new List<string>();
        using var server = new SingleInstanceCommands(value => received.Add(value), name);
        check(await SingleInstanceCommands.SendAsync("new-group", name), "New-group shell commands are acknowledged through a user-only pipe");
        check(await SingleInstanceCommands.SendAsync("settings", name) && await SingleInstanceCommands.SendAsync("toggle-panels", name), "One running instance accepts repeated menu commands");
        check(!await SingleInstanceCommands.SendAsync("arbitrary.exe", name) && received.SequenceEqual(new[] { "new-group", "settings", "toggle-panels" }), "The command channel accepts only the three supported actions");
        bool wasHidden = app.PanelsHidden;
        app.ExecuteCommand("toggle-panels");
        check(app.PanelsHidden != wasHidden, "The shell visibility action works without opening the manager");
        app.ExecuteCommand("toggle-panels");
        check(app.PanelsHidden == wasHidden, "A second shell visibility action restores the panels");
    }
}
