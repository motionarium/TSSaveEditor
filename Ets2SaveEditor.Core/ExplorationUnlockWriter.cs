using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Ets2SaveEditor.Core;

/// <summary>
/// Applies exploration unlocks (visited cities + discovered_items fog UIDs) to decrypted game.sii text.
/// </summary>
public static class ExplorationUnlockWriter
{
    public static string Apply(
        string content,
        IEnumerable<string>? extraCities,
        IEnumerable<ulong>? extraUids,
        bool markCompaniesDiscovered = true)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        string result = content;

        if (extraCities != null)
            result = MergeVisitedCities(result, extraCities);

        if (extraUids != null)
            result = MergeDiscoveredItems(result, extraUids);

        if (markCompaniesDiscovered)
            result = Regex.Replace(result, @"(?m)^(\s*)discovered:\s*false\s*$", "${1}discovered: true");

        return result;
    }

    private static string MergeVisitedCities(string content, IEnumerable<string> extraCities)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in Regex.Matches(content, @"visited_cities\[\d+\]:\s*([a-zA-Z0-9_\-]+)"))
            set.Add(m.Groups[1].Value);

        foreach (var city in extraCities)
        {
            if (!string.IsNullOrWhiteSpace(city))
                set.Add(city.Trim());
        }

        // Drop obvious junk token sometimes seen from company.volatile.*
        set.Remove("volatile");

        var list = set.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
        string indent = DetectIndent(content, "visited_cities:");
        var sb = new StringBuilder();
        sb.Append(indent).Append("visited_cities: ").Append(list.Count).Append("\r\n");
        for (int i = 0; i < list.Count; i++)
            sb.Append(indent).Append("visited_cities[").Append(i).Append("]: ").Append(list[i]).Append("\r\n");
        sb.Append(indent).Append("visited_cities_count: ").Append(list.Count).Append("\r\n");
        for (int i = 0; i < list.Count; i++)
            sb.Append(indent).Append("visited_cities_count[").Append(i).Append("]: 1\r\n");

        const string pattern =
            @"(?s)[ \t]*visited_cities:\s*\d+\r?\n(?:[ \t]*visited_cities\[\d+\]:[^\r\n]*\r?\n)+[ \t]*visited_cities_count:\s*\d+\r?\n(?:[ \t]*visited_cities_count\[\d+\]:[^\r\n]*\r?\n)+";

        if (Regex.IsMatch(content, pattern))
            return new Regex(pattern).Replace(content, sb.ToString(), 1);

        // Older / partial blocks without count array.
        const string patternNoCount =
            @"(?s)[ \t]*visited_cities:\s*\d+\r?\n(?:[ \t]*visited_cities\[\d+\]:[^\r\n]*\r?\n)+";
        if (Regex.IsMatch(content, patternNoCount))
            return new Regex(patternNoCount).Replace(content, sb.ToString(), 1);

        return content;
    }

    private static string MergeDiscoveredItems(string content, IEnumerable<ulong> extraUids)
    {
        var set = new HashSet<ulong>();
        foreach (Match m in Regex.Matches(content, @"discovered_items\[\d+\]:\s*(\d+)"))
        {
            if (ulong.TryParse(m.Groups[1].Value, out ulong uid))
                set.Add(uid);
        }

        foreach (var uid in extraUids)
            set.Add(uid);

        var list = set.OrderBy(u => u).ToList();
        string indent = DetectIndent(content, "discovered_items:");
        var sb = new StringBuilder();
        sb.Append(indent).Append("discovered_items: ").Append(list.Count).Append("\r\n");
        for (int i = 0; i < list.Count; i++)
            sb.Append(indent).Append("discovered_items[").Append(i).Append("]: ").Append(list[i]).Append("\r\n");

        const string pattern =
            @"(?s)[ \t]*discovered_items:\s*\d+\r?\n(?:[ \t]*discovered_items\[\d+\]:[^\r\n]*\r?\n)*";

        if (Regex.IsMatch(content, pattern))
            return new Regex(pattern).Replace(content, sb.ToString(), 1);

        return content;
    }

    private static string DetectIndent(string content, string key)
    {
        var m = Regex.Match(content, @"^([ \t]*)" + Regex.Escape(key), RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value : " ";
    }
}
