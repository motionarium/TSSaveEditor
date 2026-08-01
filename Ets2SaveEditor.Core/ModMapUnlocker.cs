using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TruckLib;
using TruckLib.ScsMap;

namespace Ets2SaveEditor.Core;

public sealed class ModMapScanResult
{
    public List<string> Cities { get; } = new();
    public List<ulong> DiscoverableUids { get; } = new();
    public List<string> ScannedArchives { get; } = new();
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// Reads modular map .scs archives (HashFS or ZIP): cities from /def, road UIDs from /map.
/// </summary>
public static class ModMapUnlocker
{
    private static readonly Regex CityDataRegex = new(
        @"city_data\s*:\s*city\.([a-zA-Z0-9_\-]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CityIncludeRegex = new(
        @"@include\s+""([^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ModMapScanResult Scan(
        IEnumerable<string> scsPaths,
        IProgress<ScanProgressInfo>? progress = null,
        string? preferredMapPath = null,
        bool includeMapUids = true)
    {
        var result = new ModMapScanResult();
        var citySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uidSet = new HashSet<ulong>();

        var paths = scsPaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int ai = 0; ai < paths.Count; ai++)
        {
            string path = paths[ai];
            if (!File.Exists(path))
            {
                result.Warnings.Add($"File not found: {path}");
                continue;
            }

            progress?.Report(new ScanProgressInfo
            {
                Message = Path.GetFileName(path),
                Current = ai,
                Total = Math.Max(paths.Count, 1)
            });

            try
            {
                ScanArchive(path, citySet, uidSet, result.Warnings, progress, preferredMapPath, includeMapUids, ai, paths.Count);
                result.ScannedArchives.Add(path);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        progress?.Report(new ScanProgressInfo
        {
            Message = "done",
            Current = Math.Max(paths.Count, 1),
            Total = Math.Max(paths.Count, 1)
        });

        result.Cities.AddRange(citySet.OrderBy(c => c, StringComparer.OrdinalIgnoreCase));
        result.DiscoverableUids.AddRange(uidSet.OrderBy(u => u));
        return result;
    }

    private static void ScanArchive(
        string scsPath,
        HashSet<string> cities,
        HashSet<ulong> uids,
        List<string> warnings,
        IProgress<ScanProgressInfo>? progress,
        string? preferredMapPath,
        bool includeMapUids,
        int archiveIndex,
        int archiveTotal)
    {
        using var archive = ScsArchive.Open(scsPath);
        IFileSystem fs = archive.FileSystem;

        progress?.Report(new ScanProgressInfo
        {
            Message = $"{Path.GetFileName(scsPath)} · cities",
            Current = archiveIndex,
            Total = Math.Max(archiveTotal, 1)
        });
        CollectCities(fs, cities);

        if (!includeMapUids)
            return;

        progress?.Report(new ScanProgressInfo
        {
            Message = $"{Path.GetFileName(scsPath)} · map",
            Current = 0,
            Total = 0
        });

        string fileName = Path.GetFileName(scsPath);
        bool isStockMapArchive =
            fileName.Equals("base.scs", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("base_map.scs", StringComparison.OrdinalIgnoreCase);
        string? effectivePreferred = preferredMapPath;
        if (!isStockMapArchive && MapModDetector.IsVanillaMapPath(preferredMapPath))
            effectivePreferred = null;

        foreach (var mbd in FindMapFiles(fs, effectivePreferred))
        {
            try
            {
                progress?.Report(new ScanProgressInfo
                {
                    Message = $"{Path.GetFileName(scsPath)} · {mbd}",
                    Current = 0,
                    Total = 0
                });

                var map = new UnlockScanMap
                {
                    Progress = progress,
                    ProgressLabel = $"{Path.GetFileName(scsPath)} · {Path.GetFileName(mbd)}"
                };
                map.Read(mbd, fs);
                foreach (var w in map.SectorWarnings.Take(8))
                    warnings.Add($"{Path.GetFileName(scsPath)} {mbd}: {w}");
                if (map.SectorWarnings.Count > 8)
                    warnings.Add($"{Path.GetFileName(scsPath)} {mbd}: …and {map.SectorWarnings.Count - 8} more sector errors");

                int added = 0;
                foreach (var uid in map.RoadPrefabUids)
                {
                    if (uids.Add(uid))
                        added++;
                }

                // Drop map graph immediately — do not keep sectors/items alive.
                map.MapItems.Clear();
                map.Nodes.Clear();
                map.Sectors.Clear();

                if (map.SectorWarnings.Count > 0 && added > 0)
                    warnings.Add($"{Path.GetFileName(scsPath)} {mbd}: partial read OK ({added} UIDs)");
                else if (map.SectorWarnings.Count > 0 && added == 0)
                    warnings.Add($"{Path.GetFileName(scsPath)} {mbd}: no Road/Prefab UIDs after sector errors");
            }
            catch (Exception ex)
            {
                warnings.Add($"{Path.GetFileName(scsPath)} {mbd}: {ex.Message}");
            }
            finally
            {
                // Large maps spike gen-2; encourage reclaim between archives.
                GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
            }
        }
    }

    /// <summary>
    /// UID-only map reader: keeps Road/Prefab, harvests UIDs per sector, then clears RAM.
    /// </summary>
    private sealed class UnlockScanMap : Map
    {
        public List<string> SectorWarnings { get; } = new();
        public HashSet<ulong> RoadPrefabUids { get; } = new();
        public IProgress<ScanProgressInfo>? Progress { get; set; }
        public string ProgressLabel { get; set; } = "map";

        private int _sectorIndex;
        private int _sectorTotal;

        protected override bool ShouldUpdateReferences => false;

        protected override bool PostProcessItem(MapItem item) => item is Road or Prefab;

        protected override void OnSectorLoading(Sector sector, int index, int total)
        {
            _sectorIndex = index;
            _sectorTotal = total;
            Progress?.Report(new ScanProgressInfo
            {
                Message = $"{ProgressLabel} · {sector}",
                Current = index,
                Total = Math.Max(total, 1)
            });
        }

        protected override void OnSectorLoaded(Sector sector)
        {
            HarvestAndRelease();
            ReportSectorDone();
        }

        protected override void OnSectorLoadFailed(Sector sector, Exception exception)
        {
            SectorWarnings.Add($"{sector}: {exception.Message}");
            HarvestAndRelease();
            ReportSectorDone();
        }

        private void ReportSectorDone()
        {
            Progress?.Report(new ScanProgressInfo
            {
                Message = $"{ProgressLabel} · {_sectorIndex + 1}/{_sectorTotal}",
                Current = _sectorIndex + 1,
                Total = Math.Max(_sectorTotal, 1)
            });
        }

        private void HarvestAndRelease()
        {
            foreach (var kv in MapItems)
            {
                if (kv.Value is Road or Prefab)
                    RoadPrefabUids.Add(kv.Key);
            }
            MapItems.Clear();
            Nodes.Clear();
        }
    }

    private static void CollectCities(IFileSystem fs, HashSet<string> cities)
    {
        foreach (var listPath in EnumerateLikelyCityLists(fs))
        {
            if (!TryReadText(fs, listPath, out string text))
                continue;

            string baseDir = GetDirectoryPath(listPath);
            foreach (Match m in CityIncludeRegex.Matches(text))
            {
                string includePath = ResolveInclude(baseDir, m.Groups[1].Value);
                if (TryReadText(fs, includePath, out string cityText))
                    AddCitiesFromText(cityText, cities);
            }

            AddCitiesFromText(text, cities);
        }

        try
        {
            if (!fs.DirectoryExists("/def/city"))
                return;
            foreach (var file in fs.GetFiles("/def/city"))
            {
                string full = NormalizePath(file);
                if (TryReadText(fs, full, out string cityText))
                    AddCitiesFromText(cityText, cities);
            }
        }
        catch
        {
            // Directory listing may be absent.
        }
    }

    private static IEnumerable<string> EnumerateLikelyCityLists(IFileSystem fs)
    {
        var candidates = new[]
        {
            "/def/city.sii",
            "/def/city.hex.sii",
            "/def/city.hex1.sii",
            "/def/city.promods.sii",
            "/def/city.custom.sii"
        };

        var found = new List<string>();
        foreach (var c in candidates)
        {
            if (fs.FileExists(c))
                found.Add(c);
        }

        try
        {
            if (fs.DirectoryExists("/def"))
            {
                foreach (var file in fs.GetFiles("/def"))
                {
                    string full = NormalizePath(file);
                    string name = full.Contains('/') ? full[(full.LastIndexOf('/') + 1)..] : full;
                    if (name.StartsWith("city", StringComparison.OrdinalIgnoreCase)
                        && name.EndsWith(".sii", StringComparison.OrdinalIgnoreCase)
                        && !found.Contains(full, StringComparer.OrdinalIgnoreCase))
                        found.Add(full);
                }
            }
        }
        catch { }

        return found;
    }

    private static void AddCitiesFromText(string text, HashSet<string> cities)
    {
        foreach (Match m in CityDataRegex.Matches(text))
            cities.Add(m.Groups[1].Value);
    }

    private static IEnumerable<string> FindMapFiles(IFileSystem fs, string? preferredMapPath)
    {
        var found = new List<string>();

        if (!string.IsNullOrWhiteSpace(preferredMapPath))
        {
            string? mbdHit = null;
            string? dirHit = null;
            foreach (var p in MapModDetector.ExpandMapPathCandidates(preferredMapPath))
            {
                if (p.EndsWith(".mbd", StringComparison.OrdinalIgnoreCase) && fs.FileExists(p))
                    mbdHit ??= p;
                else if (fs.DirectoryExists(p))
                    dirHit ??= p;
            }

            if (mbdHit != null)
                return new List<string> { mbdHit };
            if (dirHit != null)
                return new List<string> { dirHit };
        }

        foreach (var candidate in new[]
                 {
                     "/map/hexmap.mbd", "/map/hex.mbd", "/map/hex1.mbd",
                     "/map/europe.mbd", "/map/usa.mbd", "/map/map.mbd",
                     "/map/byy.mbd", "/map/kr.mbd", "/map/kirov.mbd"
                 })
        {
            if (fs.FileExists(candidate))
                found.Add(candidate);
        }

        try
        {
            if (fs.DirectoryExists("/map"))
            {
                foreach (var file in fs.GetFiles("/map"))
                {
                    string full = NormalizePath(file);
                    if (!full.EndsWith(".mbd", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!found.Contains(full, StringComparer.OrdinalIgnoreCase))
                        found.Add(full);
                }
            }
        }
        catch { }

        return found;
    }

    private static string NormalizePath(string path)
    {
        path = path.Replace('\\', '/').Trim();
        if (!path.StartsWith('/'))
            path = "/" + path;
        while (path.Contains("//", StringComparison.Ordinal))
            path = path.Replace("//", "/", StringComparison.Ordinal);
        return path.TrimEnd('/') is { Length: 0 } ? "/" : path.TrimEnd('/');
    }

    private static string GetDirectoryPath(string filePath)
    {
        string n = NormalizePath(filePath);
        int idx = n.LastIndexOf('/');
        return idx <= 0 ? "/" : n[..idx];
    }

    private static string ResolveInclude(string baseDir, string include)
    {
        include = include.Replace('\\', '/').Trim();
        if (include.StartsWith('/'))
            return NormalizePath(include);
        string dir = baseDir.TrimEnd('/');
        return NormalizePath(dir + "/" + include);
    }

    private static bool TryReadText(IFileSystem fs, string path, out string text)
    {
        text = "";
        try
        {
            path = NormalizePath(path);
            if (!fs.FileExists(path))
                return false;
            text = fs.ReadAllText(path, Encoding.UTF8);
            return !string.IsNullOrEmpty(text);
        }
        catch
        {
            return false;
        }
    }
}
