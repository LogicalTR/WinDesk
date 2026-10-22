using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinDesk.App;
using WinDesk.Core;

internal static class InstalledUpdateTests
{
    // Deliberately excluded from background workstation tests. Only the disposable VM
    // may save its real user workspace and start a real installer.
    internal static int Run(string requestFile)
    {
        if (Environment.MachineName != "WINDESK-QA") throw new InvalidOperationException("Real installer tests require the WinDesk QA VM.");
        var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(requestFile))!;
        var app = new App(); app.InitializeComponent();
        int exitCode = 0;
        app.Startup += (_, _) => app.Dispatcher.BeginInvoke(async () => {
            try
            {
                var version = Version.Parse(request.Version);
                string fileName = $"WinDesk-Setup-{version}-x64.exe";
                string digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(request.Installer)));
                long size = new FileInfo(request.Installer).Length;
                string metadata = JsonSerializer.Serialize(new {
                    tag_name = $"v{version}", draft = false, prerelease = false,
                    assets = new[] { new { name = fileName, state = "uploaded", size, digest = "sha256:" + digest,
                        browser_download_url = $"{ReleaseUpdates.RepositoryUrl}/releases/download/v{version}/{fileName}" } }
                });
                // The release is not public yet: serve its real bytes through a local
                // HTTP fixture. Parsing, streaming, hash checks, save, shutdown and
                // installer execution are the production implementations.
                using var http = new HttpClient(new FixtureHandler(metadata, request.Installer));
                var updates = new ReleaseUpdates(http);
                var update = await updates.CheckAsync(Version.Parse(request.PreviousVersion))
                    ?? throw new InvalidOperationException("Fixture did not offer the newer version.");
                string installer = await updates.DownloadAsync(update, Path.Combine(request.Report, "download"));
                app.Workspace.Panels[0].Name = "Kaydedilen güncelleme · 日本語";
                app.Workspace.CloseDelayMs = 1250;
                app.Workspace.Panels[0].IsPinned = true;
                app.UpdateInstallerLauncher = start => {
                    if (start.Arguments != "/SP- /NORESTART /WINDESKUPDATE=1" || start.UseShellExecute)
                        throw new InvalidOperationException("Unexpected production installer command.");
                    var saved = new WorkspaceStore(Path.Combine(UserDataLocation.DirectoryPath, "workspace.json")).Load();
                    if (saved.Panels[0].Name != app.Workspace.Panels[0].Name || saved.CloseDelayMs != 1250)
                        throw new InvalidOperationException("Installer launched before saving the workspace.");
                    // Only automation of the wizard differs: the actual release
                    // installer runs with its documented silent flags in this VM.
                    start.Arguments += $" /VERYSILENT /SUPPRESSMSGBOXES /LOG=\"{Path.Combine(request.Report, "handoff-setup.log")}\"";
                    start.CreateNoWindow = true;
                    var process = Process.Start(start) ?? throw new InvalidOperationException("Installer did not start.");
                    File.WriteAllText(Path.Combine(request.Report, "handoff.json"), JsonSerializer.Serialize(new {
                        InstallerPid = process.Id, ParentPid = Environment.ProcessId, SavedBeforeLaunch = true,
                        Version = update.Version.ToString(), Digest = digest, Size = size,
                        Layout = Path.Combine(UserDataLocation.DirectoryPath, "workspace.json"),
                        Groups = saved.Panels.Count, Shortcuts = saved.Panels.Sum(p => p.Items.Count)
                    }, new JsonSerializerOptions { WriteIndented = true }));
                    return process;
                };
                app.InstallUpdate(update, installer);
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(request.Report, "handoff-error.txt"), ex.ToString());
                exitCode = 1; app.ExitApp();
            }
        });
        app.Run();
        File.WriteAllText(Path.Combine(request.Report, "application-exited.txt"), $"IsExiting={app.IsExiting}; Code={exitCode}");
        return exitCode;
    }

    private sealed record Request(string Installer, string Version, string PreviousVersion, string Report);
    private sealed class FixtureHandler(string metadata, string installer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            HttpContent content = request.RequestUri!.AbsoluteUri == ReleaseUpdates.LatestApi
                ? new StringContent(metadata) : new StreamContent(File.OpenRead(installer));
            if (content is StreamContent) content.Headers.ContentLength = new FileInfo(installer).Length;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = content });
        }
    }
}
