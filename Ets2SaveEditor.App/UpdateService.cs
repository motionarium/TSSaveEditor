using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ets2SaveEditor.App;

internal sealed class UpdateInfo
{
    public required string TagName { get; init; }
    public required string Version { get; init; }
    public required string HtmlUrl { get; init; }
    public string? DownloadUrl { get; init; }
    public string? AssetName { get; init; }
}

internal enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    NoRelease,
    Error
}

internal sealed class UpdateCheckResult
{
    public required UpdateCheckStatus Status { get; init; }
    public UpdateInfo? Latest { get; init; }
    public string? ErrorMessage { get; init; }
}

internal static class UpdateService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("TSSaveEditor", AppInfo.Version));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public static async Task<UpdateCheckResult> CheckLatestAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await Http.GetAsync(AppInfo.ReleasesApiUrl, ct).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new UpdateCheckResult { Status = UpdateCheckStatus.NoRelease };
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;

            string tag = root.GetProperty("tag_name").GetString() ?? "";
            string version = NormalizeVersion(tag);
            string htmlUrl = root.TryGetProperty("html_url", out var hu)
                ? hu.GetString() ?? AppInfo.ReleasesPageUrl
                : AppInfo.ReleasesPageUrl;

            string? downloadUrl = null;
            string? assetName = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        continue;
                    // Prefer the published app binary name.
                    if (name.Equals("TSSaveEditor.exe", StringComparison.OrdinalIgnoreCase)
                        || downloadUrl == null)
                    {
                        downloadUrl = asset.TryGetProperty("browser_download_url", out var u)
                            ? u.GetString()
                            : null;
                        assetName = name;
                        if (name.Equals("TSSaveEditor.exe", StringComparison.OrdinalIgnoreCase))
                            break;
                    }
                }
            }

            var info = new UpdateInfo
            {
                TagName = tag,
                Version = version,
                HtmlUrl = htmlUrl,
                DownloadUrl = downloadUrl,
                AssetName = assetName
            };

            if (CompareVersions(version, AppInfo.Version) > 0)
                return new UpdateCheckResult { Status = UpdateCheckStatus.UpdateAvailable, Latest = info };

            return new UpdateCheckResult { Status = UpdateCheckStatus.UpToDate, Latest = info };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                Status = UpdateCheckStatus.Error,
                ErrorMessage = ex.Message
            };
        }
    }

    public static async Task DownloadAsync(
        string url,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long? total = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var output = new FileStream(
            destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            readTotal += read;
            if (total is > 0)
                progress?.Report(100.0 * readTotal / total.Value);
        }

        progress?.Report(100);
    }

    /// <summary>
    /// Schedules an in-place replace of the running exe after process exit, then exits the app.
    /// </summary>
    public static void ApplyAndRestart(string downloadedExePath)
    {
        string currentExe = Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, "TSSaveEditor.exe");
        string batPath = Path.Combine(Path.GetTempPath(), "TSSaveEditor_update.cmd");

        // Wait until the current process exits, then swap the binary and relaunch.
        string script =
            "@echo off\r\n" +
            "setlocal\r\n" +
            $":wait\r\n" +
            $"tasklist /FI \"PID eq {Environment.ProcessId}\" | find \"{Environment.ProcessId}\" >nul\r\n" +
            "if not errorlevel 1 (\r\n" +
            "  timeout /t 1 /nobreak >nul\r\n" +
            "  goto wait\r\n" +
            ")\r\n" +
            $"copy /Y \"{downloadedExePath}\" \"{currentExe}\" >nul\r\n" +
            $"start \"\" \"{currentExe}\"\r\n" +
            $"del \"{downloadedExePath}\" >nul 2>&1\r\n" +
            $"del \"%~f0\" >nul 2>&1\r\n";

        File.WriteAllText(batPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = batPath,
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });
    }

    public static string NormalizeVersion(string tagOrVersion)
    {
        if (string.IsNullOrWhiteSpace(tagOrVersion))
            return "0.0.0";
        string v = tagOrVersion.Trim();
        if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            v = v[1..];
        // Strip pre-release / build metadata for comparison.
        int cut = v.IndexOfAny(['-', '+']);
        if (cut >= 0) v = v[..cut];
        return v;
    }

    /// <summary>Returns &gt;0 if a is newer than b.</summary>
    public static int CompareVersions(string a, string b)
    {
        var pa = ParseParts(a);
        var pb = ParseParts(b);
        int len = Math.Max(pa.Length, pb.Length);
        for (int i = 0; i < len; i++)
        {
            int xa = i < pa.Length ? pa[i] : 0;
            int xb = i < pb.Length ? pb[i] : 0;
            if (xa != xb) return xa.CompareTo(xb);
        }
        return 0;
    }

    private static int[] ParseParts(string version)
    {
        return NormalizeVersion(version)
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => int.TryParse(p, out int n) ? n : 0)
            .ToArray();
    }
}
