using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TruckLib;

namespace Ets2SaveEditor.Core;

public sealed class MapModDetectionResult
{
    public string? MapPath { get; set; }
    public string? ArchivePath { get; set; }
    public string? ModId { get; set; }
    public string? DisplayName { get; set; }
    public bool IsVanilla { get; set; }
    public List<string> CandidateIds { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool Found => !string.IsNullOrEmpty(ArchivePath);
}

/// <summary>
/// Resolves which .scs (or base.scs) owns the profile map_path by EntryExists checks.
/// </summary>
public static class MapModDetector
{
    private static readonly Regex MapPathRegex = new(
        @"map_path\s*:\s*""([^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ActiveModRegex = new(
        @"active_mods\[\d+\]\s*:\s*""([^""|]+)\|([^""]*)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DependencyModRegex = new(
        @"dependencies\[\d+\]\s*:\s*""mod\|([^""|]+)\|([^""]*)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> VanillaMapPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/map/europe.mbd",
        "/map/usa.mbd",
        "/map/map.mbd"
    };

    public static MapModDetectionResult Detect(string saveFolder, string? profileFolder = null)
    {
        var result = new MapModDetectionResult();
        profileFolder ??= FindProfileFolder(saveFolder);

        var candidates = new List<(string Id, string Name)>();

        if (!string.IsNullOrEmpty(profileFolder))
        {
            string profileSii = Path.Combine(profileFolder, "profile.sii");
            string? profileText = TryDecrypt(profileSii);
            if (!string.IsNullOrEmpty(profileText))
            {
                var mp = MapPathRegex.Match(profileText);
                if (mp.Success)
                    result.MapPath = mp.Groups[1].Value.Trim();

                foreach (Match m in ActiveModRegex.Matches(profileText))
                    AddCandidate(candidates, m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim());
            }
            else
            {
                result.Warnings.Add("Could not read profile.sii");
            }
        }

        string infoSii = Path.Combine(saveFolder, "info.sii");
        string? infoText = TryDecrypt(infoSii);
        if (!string.IsNullOrEmpty(infoText))
        {
            foreach (Match m in DependencyModRegex.Matches(infoText))
                AddCandidate(candidates, m.Groups[1].Value.Trim(), m.Groups[2].Value.Trim());
        }

        foreach (var c in candidates)
            result.CandidateIds.Add(c.Id);

        if (string.IsNullOrEmpty(result.MapPath))
        {
            result.Warnings.Add("map_path not found in profile");
            return result;
        }

        result.IsVanilla = IsVanillaMapPath(result.MapPath);

        var searchRoots = BuildModSearchRoots();
        var archiveCandidates = new List<(string Path, string? ModId, string? Name, bool Vanilla)>();

        bool likelyEts2 = result.MapPath.Contains("europe", StringComparison.OrdinalIgnoreCase)
            || result.MapPath.Contains("hex", StringComparison.OrdinalIgnoreCase);
        bool likelyAts = result.MapPath.Contains("usa", StringComparison.OrdinalIgnoreCase);

        // Vanilla map_path (/map/europe.mbd etc.): prefer game base.scs.
        // Map mods often also ship europe.mbd — picking the first active_mod (e.g. Kirov) is wrong.
        if (result.IsVanilla)
        {
            foreach (var baseScs in FindBaseScs(likelyEts2, likelyAts))
                archiveCandidates.Add((baseScs, null, Path.GetFileName(baseScs), true));
        }

        foreach (var (id, name) in candidates)
        {
            foreach (var file in ResolveModArchives(id, searchRoots))
                archiveCandidates.Add((file, id, name, false));
        }

        if (!result.IsVanilla)
        {
            foreach (var baseScs in FindBaseScs(likelyEts2, likelyAts))
                archiveCandidates.Add((baseScs, null, Path.GetFileName(baseScs), true));
        }

        foreach (var cand in archiveCandidates
                     .GroupBy(a => a.Path, StringComparer.OrdinalIgnoreCase)
                     .Select(g => g.First()))
        {
            try
            {
                if (!ArchiveContains(cand.Path, result.MapPath))
                    continue;

                result.ArchivePath = cand.Path;
                result.ModId = cand.ModId;
                result.DisplayName = ModVersioning.SanitizeText(
                    string.IsNullOrWhiteSpace(cand.Name)
                        ? cand.ModId ?? Path.GetFileName(cand.Path)
                        : cand.Name);
                result.IsVanilla = cand.Vanilla || VanillaMapPaths.Contains(result.MapPath);
                return result;
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"{Path.GetFileName(cand.Path)}: {ex.Message}");
            }
        }

        result.Warnings.Add($"No archive contains {result.MapPath}");
        return result;
    }

    /// <summary>
    /// Resolves on-disk paths for the map owner plus all active_mods / dependency candidates.
    /// </summary>
    public static List<string> ResolveScanArchives(MapModDetectionResult detect, string? archiveOverride = null)
    {
        var paths = new List<string>();
        var roots = BuildModSearchRoots();

        void Add(string? p)
        {
            if (!string.IsNullOrWhiteSpace(p) && File.Exists(p)
                && !paths.Contains(p, StringComparer.OrdinalIgnoreCase))
                paths.Add(p);
        }

        Add(archiveOverride);
        Add(detect.ArchivePath);

        foreach (var id in detect.CandidateIds)
        {
            foreach (var file in ResolveModArchives(id, roots))
                Add(file);
        }

        return paths;
    }

    public static string? FindProfileFolder(string saveFolder)
    {
        try
        {
            var di = new DirectoryInfo(saveFolder);
            if (di.Parent?.Name.Equals("save", StringComparison.OrdinalIgnoreCase) == true)
                return di.Parent.Parent?.FullName;
        }
        catch { }
        return null;
    }

    private static void AddCandidate(List<(string Id, string Name)> list, string id, string name)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        id = ModVersioning.SanitizeText(id);
        name = ModVersioning.SanitizeText(name);
        if (string.IsNullOrWhiteSpace(id)) return;
        if (list.Any(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase))) return;
        list.Add((id, name));
    }

    private static string? TryDecrypt(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            return SaveEngine.DecryptFile(path);
        }
        catch
        {
            return null;
        }
    }

    private static bool ArchiveContains(string archivePath, string mapPath)
    {
        try
        {
            using var archive = ScsArchive.Open(archivePath);
            IFileSystem fs = archive.FileSystem;
            foreach (var candidate in ExpandMapPathCandidates(mapPath))
            {
                if (fs.FileExists(candidate) || fs.DirectoryExists(candidate))
                    return true;
            }
        }
        catch
        {
            return false;
        }
        return false;
    }

    internal static IEnumerable<string> ExpandMapPathCandidates(string mapPath)
    {
        string n = mapPath.Replace('\\', '/').Trim();
        if (!n.StartsWith('/')) n = "/" + n;
        yield return n;
        if (n.EndsWith(".mbd", StringComparison.OrdinalIgnoreCase))
            yield return n[..^4];
        else
            yield return n + ".mbd";
    }

    private static IEnumerable<string> ResolveModArchives(string modId, IEnumerable<string> roots)
    {
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;

            string scs = Path.Combine(root, modId + ".scs");
            if (File.Exists(scs))
                yield return scs;

            foreach (var nested in Directory.EnumerateFiles(root, "*.scs", SearchOption.TopDirectoryOnly))
            {
                if (Path.GetFileNameWithoutExtension(nested).Equals(modId, StringComparison.OrdinalIgnoreCase))
                    yield return nested;
            }
        }
    }

    private static List<string> BuildModSearchRoots()
    {
        var roots = new List<string>();
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        foreach (var game in new[] { "Euro Truck Simulator 2", "American Truck Simulator" })
        {
            string mod = Path.Combine(docs, game, "mod");
            if (Directory.Exists(mod)) roots.Add(mod);
        }

        string? steam = PathScanner.GetSteamPath();
        if (!string.IsNullOrEmpty(steam))
        {
            foreach (var appId in new[] { "227300", "270880" })
            {
                string ws = Path.Combine(steam, "steamapps", "workshop", "content", appId);
                if (!Directory.Exists(ws)) continue;
                foreach (var sub in Directory.EnumerateDirectories(ws))
                    roots.Add(sub);
            }
        }

        return roots;
    }

    private static IEnumerable<string> FindBaseScs(bool preferEts2, bool preferAts)
    {
        var games = new List<string>();
        if (preferAts) games.Add("American Truck Simulator");
        if (preferEts2) games.Add("Euro Truck Simulator 2");
        foreach (var g in new[] { "Euro Truck Simulator 2", "American Truck Simulator" })
            if (!games.Contains(g)) games.Add(g);

        var libraryRoots = new List<string>();
        string? steam = PathScanner.GetSteamPath();
        if (!string.IsNullOrEmpty(steam))
        {
            libraryRoots.Add(Path.Combine(steam, "steamapps", "common"));
            string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                {
                    string lib = m.Groups[1].Value.Replace("\\\\", "\\");
                    libraryRoots.Add(Path.Combine(lib, "steamapps", "common"));
                }
            }
        }

        // Extra common Steam library locations (registry may be empty on some setups).
        foreach (var drive in new[] { "C", "D", "E", "F" })
        {
            libraryRoots.Add($@"{drive}:\Program Files (x86)\Steam\steamapps\common");
            libraryRoots.Add($@"{drive}:\Program Files\Steam\steamapps\common");
            libraryRoots.Add($@"{drive}:\SteamLibrary\steamapps\common");
            libraryRoots.Add($@"{drive}:\Steam\steamapps\common");
            libraryRoots.Add($@"{drive}:\Games\Steam\steamapps\common");
        }

        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in games)
        {
            foreach (var lib in libraryRoots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                // Newer ETS2/ATS: map lives in base_map.scs; older builds: base.scs.
                foreach (var fileName in new[] { "base_map.scs", "base.scs" })
                {
                    string path = Path.Combine(lib, game, fileName);
                    if (!File.Exists(path) || !yielded.Add(path))
                        continue;
                    yield return path;
                }
            }
        }
    }

    /// <summary>True if path is the stock ETS2/ATS map module.</summary>
    public static bool IsVanillaMapPath(string? mapPath)
    {
        if (string.IsNullOrWhiteSpace(mapPath)) return false;
        string n = mapPath.Replace('\\', '/').Trim();
        if (!n.StartsWith('/')) n = "/" + n;
        return VanillaMapPaths.Contains(n)
            || n.Equals("/map/europe", StringComparison.OrdinalIgnoreCase)
            || n.Equals("/map/usa", StringComparison.OrdinalIgnoreCase);
    }
}
