using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ets2SaveEditor.Core;

public sealed class ModFolderEntry
{
    public string Id { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime LastWriteUtc { get; set; }

    /// <summary>True if manifest.sii was read (or confirmed missing).</summary>
    public bool ManifestParsed { get; set; }

    /// <summary>Empty after parse = all game versions.</summary>
    public List<string> CompatibleVersions { get; set; } = new();

    public string? PackageVersion { get; set; }
    public string? ManifestDisplayName { get; set; }

    public string SizeLabel
    {
        get
        {
            if (SizeBytes < 1024) return $"{SizeBytes} B";
            if (SizeBytes < 1024 * 1024) return $"{SizeBytes / 1024.0:0.#} KB";
            if (SizeBytes < 1024L * 1024 * 1024) return $"{SizeBytes / (1024.0 * 1024):0.#} MB";
            return $"{SizeBytes / (1024.0 * 1024 * 1024):0.##} GB";
        }
    }

    public string VersionsLabel => ModVersioning.FormatVersionsLabel(this);
}

public sealed class ActiveModEntry
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Source { get; set; } = ""; // profile | info
    public List<string> CompatibleVersions { get; set; } = new();
    public bool ManifestParsed { get; set; }
    public bool? CompatibleWithGame { get; set; }

    public string VersionsLabel
    {
        get
        {
            if (!ManifestParsed) return "";
            if (CompatibleVersions == null || CompatibleVersions.Count == 0) return "все версии";
            return string.Join(", ", CompatibleVersions);
        }
    }
}

public sealed class ModCatalogCache
{
    public string? ModFolder { get; set; }
    public DateTime ScannedAtUtc { get; set; }
    public List<ModFolderEntry> Mods { get; set; } = new();
}

public sealed class ModCatalogSnapshot
{
    public string? ModFolder { get; set; }
    public DateTime? ScannedAtUtc { get; set; }
    public string? GameVersion { get; set; }
    public List<ModFolderEntry> FolderMods { get; } = new();
    public List<ActiveModEntry> ActiveMods { get; } = new();
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// Scans the Documents/.../mod folder, caches results, and lists active mods from the save/profile.
/// View-only for now (no writing active_mods).
/// </summary>
public static class ModCatalog
{
    private static readonly Regex ActiveModRegex = new(
        @"active_mods\[\d+\]\s*:\s*""([^""|]+)\|([^""]*)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DependencyModRegex = new(
        @"dependencies\[\d+\]\s*:\s*""mod\|([^""|]+)\|([^""]*)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string DefaultCachePath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mods_cache.json");

    public static string? ResolveDefaultModFolder(bool preferAts = false)
    {
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string primary = preferAts ? "American Truck Simulator" : "Euro Truck Simulator 2";
        string secondary = preferAts ? "Euro Truck Simulator 2" : "American Truck Simulator";
        foreach (var game in new[] { primary, secondary })
        {
            string mod = Path.Combine(docs, game, "mod");
            if (Directory.Exists(mod))
                return mod;
        }
        return Path.Combine(docs, primary, "mod");
    }

    public static ModCatalogCache ScanFolder(string modFolder, IProgress<ScanProgressInfo>? progress = null)
    {
        var cache = new ModCatalogCache
        {
            ModFolder = modFolder,
            ScannedAtUtc = DateTime.UtcNow
        };

        if (!Directory.Exists(modFolder))
            return cache;

        var files = Directory.EnumerateFiles(modFolder, "*.scs", SearchOption.TopDirectoryOnly)
            .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int i = 0; i < files.Count; i++)
        {
            string path = files[i];
            var fi = new FileInfo(path);
            var entry = new ModFolderEntry
            {
                Id = Path.GetFileNameWithoutExtension(path),
                FileName = fi.Name,
                FullPath = fi.FullName,
                SizeBytes = fi.Length,
                LastWriteUtc = fi.LastWriteTimeUtc
            };
            ModVersioning.FillFromManifest(entry);
            cache.Mods.Add(entry);

            progress?.Report(new ScanProgressInfo
            {
                Message = fi.Name,
                Current = i + 1,
                Total = files.Count
            });
        }

        return cache;
    }

    public static void SaveCache(ModCatalogCache cache, string? path = null)
    {
        path ??= DefaultCachePath;
        File.WriteAllText(path, JsonSerializer.Serialize(cache, JsonOpts));
    }

    public static ModCatalogCache? LoadCache(string? path = null)
    {
        path ??= DefaultCachePath;
        if (!File.Exists(path))
            return null;
        try
        {
            return JsonSerializer.Deserialize<ModCatalogCache>(File.ReadAllText(path), JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public static List<ActiveModEntry> ReadActiveMods(string saveFolder, string? profileFolder = null)
    {
        var list = new List<ActiveModEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        profileFolder ??= MapModDetector.FindProfileFolder(saveFolder);

        if (!string.IsNullOrEmpty(profileFolder))
        {
            string? profileText = TryDecrypt(Path.Combine(profileFolder, "profile.sii"));
            if (!string.IsNullOrEmpty(profileText))
            {
                foreach (Match m in ActiveModRegex.Matches(profileText))
                {
                    string id = m.Groups[1].Value.Trim();
                    if (!seen.Add(id)) continue;
                    list.Add(new ActiveModEntry
                    {
                        Id = ModVersioning.SanitizeText(id),
                        DisplayName = ModVersioning.SanitizeText(m.Groups[2].Value),
                        Source = "profile"
                    });
                }
            }
        }

        string? infoText = TryDecrypt(Path.Combine(saveFolder, "info.sii"));
        if (!string.IsNullOrEmpty(infoText))
        {
            foreach (Match m in DependencyModRegex.Matches(infoText))
            {
                string id = m.Groups[1].Value.Trim();
                if (!seen.Add(id)) continue;
                list.Add(new ActiveModEntry
                {
                    Id = ModVersioning.SanitizeText(id),
                    DisplayName = ModVersioning.SanitizeText(m.Groups[2].Value),
                    Source = "info"
                });
            }
        }

        return list.OrderBy(m => m.DisplayName.Length > 0 ? m.DisplayName : m.Id,
            StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static ModCatalogSnapshot BuildSnapshot(
        string? modFolder,
        string? saveFolder,
        bool rescan,
        string? cachePath = null,
        IProgress<ScanProgressInfo>? progress = null,
        bool preferAts = false,
        string? gameVersion = null)
    {
        var snap = new ModCatalogSnapshot();
        cachePath ??= DefaultCachePath;
        snap.GameVersion = gameVersion ?? ModVersioning.DetectGameVersion(preferAts);

        ModCatalogCache? cache = null;
        if (!rescan)
            cache = LoadCache(cachePath);

        // Old caches without manifest data → force a light re-read of manifests.
        bool needManifestEnrich = cache?.Mods?.Any(m => !m.ManifestParsed) == true;

        if (string.IsNullOrWhiteSpace(modFolder))
            modFolder = cache?.ModFolder ?? ResolveDefaultModFolder(preferAts);

        snap.ModFolder = modFolder;

        if (rescan || cache == null || !string.Equals(cache.ModFolder, modFolder, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(modFolder))
            {
                snap.Warnings.Add("Mod folder not set");
            }
            else if (!Directory.Exists(modFolder))
            {
                snap.Warnings.Add($"Mod folder not found: {modFolder}");
                cache = new ModCatalogCache { ModFolder = modFolder, ScannedAtUtc = DateTime.UtcNow };
                SaveCache(cache, cachePath);
            }
            else
            {
                cache = ScanFolder(modFolder, progress);
                SaveCache(cache, cachePath);
                needManifestEnrich = false;
            }
        }
        else if (needManifestEnrich && cache != null)
        {
            for (int i = 0; i < cache.Mods.Count; i++)
            {
                var m = cache.Mods[i];
                if (!m.ManifestParsed)
                    ModVersioning.FillFromManifest(m);
                progress?.Report(new ScanProgressInfo
                {
                    Message = m.FileName,
                    Current = i + 1,
                    Total = cache.Mods.Count
                });
            }
            cache.ScannedAtUtc = DateTime.UtcNow;
            SaveCache(cache, cachePath);
        }

        if (cache != null)
        {
            snap.ScannedAtUtc = cache.ScannedAtUtc;
            snap.FolderMods.AddRange(cache.Mods);
        }

        if (!string.IsNullOrEmpty(saveFolder) && Directory.Exists(saveFolder))
        {
            try
            {
                var active = ReadActiveMods(saveFolder);
                var byId = snap.FolderMods.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
                foreach (var a in active)
                {
                    if (byId.TryGetValue(a.Id, out var folder))
                    {
                        a.ManifestParsed = folder.ManifestParsed;
                        a.CompatibleVersions = folder.CompatibleVersions?.ToList() ?? new List<string>();
                        a.CompatibleWithGame = ModVersioning.IsCompatibleWith(folder, snap.GameVersion);
                    }
                    snap.ActiveMods.Add(a);
                }
            }
            catch (Exception ex)
            {
                snap.Warnings.Add($"Active mods: {ex.Message}");
            }
        }

        return snap;
    }

    private static string? TryDecrypt(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return SaveEngine.DecryptFile(path);
        }
        catch
        {
            return null;
        }
    }
}
