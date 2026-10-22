using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WinDesk.Core;

public sealed record ReleaseUpdate(Version Version, Uri ReleaseUrl, Uri DownloadUrl, string FileName, long Size, string Sha256);

/// <summary>Public, stable releases only. No account, token or workspace data is sent.</summary>
public sealed class ReleaseUpdates(HttpClient http)
{
    public const string RepositoryUrl = "https://github.com/LogicalTR/WinDesk";
    public const string LatestApi = "https://api.github.com/repos/LogicalTR/WinDesk/releases/latest";
    private const long MaxInstallerSize = 256 * 1024 * 1024;

    public static ReleaseUpdate? Parse(string json, Version current)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(tag, @"^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$", RegexOptions.CultureInvariant)
            || !Version.TryParse(tag[1..], out var version))
            throw new InvalidDataException("Yayın sürümü geçerli değil.");
        // Assembly versions have four components; compare the three release components.
        if (version <= new Version(current.Major, current.Minor, Math.Max(0, current.Build))) return null;
        string name = $"WinDesk-Setup-{version}-x64.exe";
        var assets = root.GetProperty("assets").EnumerateArray()
            .Where(a => a.GetProperty("name").GetString() == name).ToArray();
        if (assets.Length != 1) throw new InvalidDataException("Bu sürümün x64 kurucusu henüz hazır değil.");
        var asset = assets[0];
        string digest = asset.GetProperty("digest").GetString() ?? "";
        string expectedUrl = $"{RepositoryUrl}/releases/download/{tag}/{name}";
        if (asset.GetProperty("state").GetString() != "uploaded"
            || asset.GetProperty("browser_download_url").GetString() != expectedUrl
            || !Regex.IsMatch(digest, "^sha256:[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("Kurucu bağlantısı veya doğrulama bilgisi geçerli değil.");
        var update = new ReleaseUpdate(version, new Uri($"{RepositoryUrl}/releases/tag/{tag}"),
            new Uri(expectedUrl), name, asset.GetProperty("size").GetInt64(), digest[7..]);
        Validate(update);
        return update;
    }

    public async Task<ReleaseUpdate?> CheckAsync(Version current, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestApi);
        request.Headers.UserAgent.ParseAdd($"WinDesk/{current.ToString(3)}");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        // A release body must not cause unbounded allocation.
        await response.Content.LoadIntoBufferAsync(1024 * 1024, timeout.Token).ConfigureAwait(false);
        return Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false), current);
    }

    public async Task<string> DownloadAsync(ReleaseUpdate update, string directory,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        Validate(update);
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, update.FileName);
        string partial = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, update.DownloadUrl);
            request.Headers.UserAgent.ParseAdd("WinDesk-Updater");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps
                || (response.Content.Headers.ContentLength is long length && length != update.Size))
                throw new InvalidDataException("İndirme boyutu veya bağlantısı beklenenle eşleşmiyor.");
            await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false))
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[65536];
                long total = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) > 0)
                {
                    total += count;
                    if (total > update.Size) throw new InvalidDataException("İndirme beklenen boyutu aştı.");
                    hash.AppendData(buffer, 0, count);
                    await output.WriteAsync(buffer.AsMemory(0, count), timeout.Token).ConfigureAwait(false);
                    progress?.Report((double)total / update.Size);
                }
                if (total != update.Size || !CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), Convert.FromHexString(update.Sha256)))
                    throw new InvalidDataException("İndirme doğrulanamadı. Dosya çalıştırılmadı; yeniden deneyin.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(partial, destination); // Never replace an existing download.
            return destination;
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }

    private static void Validate(ReleaseUpdate update)
    {
        string name = $"WinDesk-Setup-{update.Version.ToString(3)}-x64.exe";
        if (update.Version.Revision >= 0 || update.FileName != name
            || update.DownloadUrl.AbsoluteUri != $"{RepositoryUrl}/releases/download/v{update.Version}/{name}"
            || update.Size <= 0 || update.Size > MaxInstallerSize
            || !Regex.IsMatch(update.Sha256, "^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("Güncelleme paketi geçerli değil.");
    }

    public static FileStream OpenVerifiedInstaller(ReleaseUpdate update, string path)
    {
        Validate(update);
        if (Path.GetFileName(path) != update.FileName) throw new InvalidDataException("Kurucu dosyası eşleşmiyor.");
        var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (file.Length != update.Size || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(file), Convert.FromHexString(update.Sha256)))
                throw new InvalidDataException("Kurucu indirmeden sonra değişmiş. Yeniden indirmeniz gerekiyor.");
            file.Position = 0;
            return file; // Keep the verified bytes read-locked until Process.Start returns.
        }
        catch { file.Dispose(); throw; }
    }
}
