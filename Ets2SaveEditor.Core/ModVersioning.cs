using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TruckLib;

namespace Ets2SaveEditor.Core;

/// <summary>Reads mod manifest.sii and matches SCS compatible_versions patterns.</summary>
public static class ModVersioning
{
    private static readonly Regex CompatibleVersionRegex = new(
        @"compatible_versions\s*(?:\[\d+\])?\s*:\s*""([^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DisplayNameRegex = new(
        @"display_name\s*:\s*""([^""]*)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PackageVersionRegex = new(
        @"package_version\s*:\s*""([^""]*)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Modern logs: "[ufs] Loaded pack set version 1.57.2.7 created at …"
    private static readonly Regex PackSetVersionRegex = new(
        @"Loaded\s+pack\s+set\s+version\s+([0-9]+(?:\.[0-9]+)+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "uset g_game_version ""1.50.1.0"""
    private static readonly Regex UsetGameVersionRegex = new(
        @"uset\s+g_game_version\s+""([0-9]+(?:\.[0-9]+)+(?:s)?)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // "Euro Truck Simulator 2 init ver.1.50.1.0s (rev. …)"
    private static readonly Regex InitVerRegex = new(
        @"init\s+ver\.?\s*([0-9]+(?:\.[0-9]+)+(?:s)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Older / alternate: "version info: 1.49.3.12s" or "Version info: ETS2 1.49…"
    private static readonly Regex GameLogVersionRegex = new(
        @"version\s+info\s*:\s*(?:[^\r\n]*?)([0-9]+(?:\.[0-9]+)+(?:s)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Detects installed game version from Documents\Euro Truck Simulator 2\game.log.txt
    /// (or American Truck Simulator). Also checks OneDrive Documents mirrors.
    /// </summary>
    public static string? DetectGameVersion(bool preferAts = false)
    {
        string primary = preferAts ? "American Truck Simulator" : "Euro Truck Simulator 2";
        string secondary = preferAts ? "Euro Truck Simulator 2" : "American Truck Simulator";

        foreach (var game in new[] { primary, secondary })
        {
            foreach (var root in EnumerateGameDocumentRoots(game))
            {
                string? v = TryReadVersionFromLog(Path.Combine(root, "game.log.txt"));
                if (!string.IsNullOrEmpty(v))
                    return v;
            }
        }

        foreach (var game in new[] { primary, secondary })
        {
            foreach (var root in EnumerateGameDocumentRoots(game))
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(root, "game.log.txt*")
                                 .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc))
                    {
                        string? v = TryReadVersionFromLog(file);
                        if (!string.IsNullOrEmpty(v))
                            return v;
                    }
                }
                catch { /* ignore */ }
            }
        }

        return null;
    }

    /// <summary>
    /// Documents\Euro Truck Simulator 2 and Documents\American Truck Simulator
    /// (MyDocuments + common USERPROFILE / OneDrive mirrors).
    /// </summary>
    public static IEnumerable<string> EnumerateGameDocumentRoots(string gameFolderName)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var docs in EnumerateDocumentsRoots())
        {
            string root = Path.Combine(docs, gameFolderName);
            if (!seen.Add(root)) continue;
            if (Directory.Exists(root))
                yield return root;
        }
    }

    private static IEnumerable<string> EnumerateDocumentsRoots()
    {
        var list = new List<string>();
        void Push(string? p)
        {
            if (string.IsNullOrWhiteSpace(p)) return;
            try { p = Path.GetFullPath(p.Trim()); } catch { return; }
            if (!list.Any(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase))
                && Directory.Exists(p))
                list.Add(p);
        }

        Push(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Push(Environment.GetFolderPath(Environment.SpecialFolder.Personal));

        string? profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(profile))
        {
            Push(Path.Combine(profile, "Documents"));
            Push(Path.Combine(profile, "OneDrive", "Documents"));
            Push(Path.Combine(profile, "OneDrive", "Документы"));
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(profile, "OneDrive*"))
                {
                    Push(Path.Combine(dir, "Documents"));
                    Push(Path.Combine(dir, "Документы"));
                }
            }
            catch { /* ignore */ }
        }

        return list;
    }

    public static string? TryReadVersionFromLog(string logPath)
    {
        if (!File.Exists(logPath)) return null;
        try
        {
            using var fs = File.Open(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            // Version lines are near the top; 256 KB is enough even for noisy logs.
            int toRead = (int)Math.Min(fs.Length, 256 * 1024);
            var buf = new byte[toRead];
            int read = fs.Read(buf, 0, toRead);
            string text = Encoding.UTF8.GetString(buf, 0, read);
            if (text.IndexOf('\0') >= 0)
                text = Encoding.Latin1.GetString(buf, 0, read);

            return ExtractVersionFromLogText(text);
        }
        catch
        {
            return null;
        }
    }

    internal static string? ExtractVersionFromLogText(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;

        // Prefer explicit game version cvar / init line; pack set is a good fallback.
        foreach (var rx in new[] { UsetGameVersionRegex, InitVerRegex, PackSetVersionRegex, GameLogVersionRegex })
        {
            var m = rx.Match(text);
            if (m.Success)
                return NormalizeGameVersion(m.Groups[1].Value);
        }

        return null;
    }

    private static string NormalizeGameVersion(string raw)
    {
        string v = raw.Trim();
        // Keep trailing 's' if present (steam build marker used in some logs).
        return v;
    }

    /// <summary>
    /// Cleans mod/display names: decodes literal \xNN escapes, strips zero-width chars.
    /// </summary>
    public static string SanitizeText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        string s = text;
        if (s.Contains("\\x", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = new List<byte>();
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length;)
            {
                if (i + 3 < s.Length
                    && s[i] == '\\'
                    && (s[i + 1] == 'x' || s[i + 1] == 'X')
                    && IsHex(s[i + 2]) && IsHex(s[i + 3]))
                {
                    bytes.Add(Convert.ToByte(s.Substring(i + 2, 2), 16));
                    i += 4;
                    continue;
                }

                FlushUtf8Bytes(bytes, sb);
                sb.Append(s[i]);
                i++;
            }
            FlushUtf8Bytes(bytes, sb);
            s = sb.ToString();
        }

        s = s.Replace("\u200B", "")
             .Replace("\u200C", "")
             .Replace("\u200D", "")
             .Replace("\uFEFF", "")
             .Replace('\u00A0', ' ');

        return Regex.Replace(s.Trim(), @"\s+", " ");
    }

    private static void FlushUtf8Bytes(List<byte> bytes, StringBuilder sb)
    {
        if (bytes.Count == 0) return;
        sb.Append(Encoding.UTF8.GetString(bytes.ToArray()));
        bytes.Clear();
    }

    private static bool IsHex(char c) =>
        (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    public static void FillFromManifest(ModFolderEntry entry)
    {
        entry.ManifestParsed = false;
        entry.CompatibleVersions = new List<string>();
        entry.PackageVersion = null;
        entry.ManifestDisplayName = null;

        if (string.IsNullOrEmpty(entry.FullPath) || !File.Exists(entry.FullPath))
            return;

        try
        {
            using var archive = ScsArchive.Open(entry.FullPath);
            IFileSystem fs = archive.FileSystem;
            string? text = null;
            foreach (var candidate in new[] { "/manifest.sii", "manifest.sii", "/manifest.SII" })
            {
                if (!fs.FileExists(candidate)) continue;
                text = fs.ReadAllText(candidate);
                break;
            }

            if (string.IsNullOrEmpty(text))
            {
                // No manifest → treat as all versions (same as SCS when attribute omitted).
                entry.ManifestParsed = true;
                return;
            }

            entry.ManifestParsed = true;
            var dn = DisplayNameRegex.Match(text);
            if (dn.Success) entry.ManifestDisplayName = SanitizeText(dn.Groups[1].Value);
            var pv = PackageVersionRegex.Match(text);
            if (pv.Success) entry.PackageVersion = SanitizeText(pv.Groups[1].Value);

            foreach (Match m in CompatibleVersionRegex.Matches(text))
            {
                string pat = SanitizeText(m.Groups[1].Value);
                if (pat.Length > 0 && !entry.CompatibleVersions.Contains(pat, StringComparer.OrdinalIgnoreCase))
                    entry.CompatibleVersions.Add(pat);
            }
        }
        catch
        {
            // Unreadable archive — leave ManifestParsed false (unknown).
        }
    }

    /// <summary>
    /// Empty compatible list (after successful parse) means "all versions".
    /// Unknown manifest → compatible (do not hide).
    /// </summary>
    public static bool IsCompatibleWith(ModFolderEntry entry, string? gameVersion)
    {
        if (string.IsNullOrWhiteSpace(gameVersion))
            return true;
        if (!entry.ManifestParsed)
            return true;
        if (entry.CompatibleVersions == null || entry.CompatibleVersions.Count == 0)
            return true;

        string normGame = NormalizeVersion(gameVersion);
        foreach (var pat in entry.CompatibleVersions)
        {
            if (MatchesPattern(normGame, pat))
                return true;
        }
        return false;
    }

    public static string FormatVersionsLabel(ModFolderEntry entry)
    {
        // Empty = unknown (UI localizes). "все версии" kept for cache/debug; UI prefers Loc keys.
        if (!entry.ManifestParsed)
            return "";
        if (entry.CompatibleVersions == null || entry.CompatibleVersions.Count == 0)
            return "все версии";
        return string.Join(", ", entry.CompatibleVersions);
    }

    public static string NormalizeVersion(string version)
    {
        string v = version.Trim();
        if (v.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            v = v[..^1];
        return v;
    }

    public static bool MatchesPattern(string normalizedGameVersion, string pattern)
    {
        string pat = NormalizeVersion(pattern);
        var vParts = normalizedGameVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var pParts = pat.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (pParts.Length == 0) return false;

        for (int i = 0; i < pParts.Length; i++)
        {
            if (pParts[i] == "*")
                return true;
            if (i >= vParts.Length)
                return false;
            if (!string.Equals(pParts[i], vParts[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Exact pattern length: "1.50.1" does not match "1.50.1.2"
        return pParts.Length == vParts.Length;
    }
}
