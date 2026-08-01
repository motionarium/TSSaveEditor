using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using TruckLib;

namespace Ets2SaveEditor.Core;

/// <summary>
/// Read-only <see cref="IFileSystem"/> over a ZIP-based .scs archive (PK header).
/// Paths use forward slashes and a leading '/', e.g. /map/byy.mbd.
/// </summary>
public sealed class ZipScsFileSystem : IFileSystem, IDisposable
{
    private readonly ZipArchive _zip;
    private readonly FileStream _stream;
    private readonly Dictionary<string, ZipArchiveEntry> _files;
    private readonly HashSet<string> _dirs;
    private bool _disposed;

    public char DirectorySeparator => '/';

    public ZipScsFileSystem(string path)
    {
        _stream = File.OpenRead(path);
        _zip = new ZipArchive(_stream, ZipArchiveMode.Read, leaveOpen: false);
        _files = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        _dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "/" };

        foreach (var entry in _zip.Entries)
        {
            string norm = NormalizeEntryPath(entry.FullName);
            if (string.IsNullOrEmpty(norm) || norm == "/")
                continue;

            bool isDir = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\') || entry.Length == 0 && !Path.HasExtension(entry.FullName);
            if (isDir)
            {
                AddDir(norm.TrimEnd('/'));
                continue;
            }

            _files[norm] = entry;
            AddDir(GetParent(norm) ?? "/");
            // Ensure all parent dirs exist
            string? parent = GetParent(norm);
            while (!string.IsNullOrEmpty(parent) && parent != "/")
            {
                AddDir(parent);
                parent = GetParent(parent);
            }
        }
    }

    public static bool IsZipArchive(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            if (fs.Length < 4) return false;
            int b0 = fs.ReadByte();
            int b1 = fs.ReadByte();
            return b0 == 'P' && b1 == 'K';
        }
        catch
        {
            return false;
        }
    }

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public bool DirectoryExists(string path)
    {
        string n = Normalize(path).TrimEnd('/');
        if (n.Length == 0) n = "/";
        if (_dirs.Contains(n)) return true;
        // Prefix match for dirs that only exist implicitly
        string prefix = n.EndsWith('/') ? n : n + "/";
        return _files.Keys.Any(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public IList<string> GetFiles(string path)
    {
        string dir = Normalize(path).TrimEnd('/');
        if (dir.Length == 0) dir = "/";
        string prefix = dir == "/" ? "/" : dir + "/";

        var list = new List<string>();
        foreach (var file in _files.Keys)
        {
            if (!file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            string rest = file.Substring(prefix.Length);
            if (rest.Contains('/'))
                continue; // only direct children
            list.Add(file);
        }
        return list;
    }

    public string GetParent(string path)
    {
        string n = Normalize(path).TrimEnd('/');
        if (n == "/" || string.IsNullOrEmpty(n))
            return null!;
        int idx = n.LastIndexOf('/');
        if (idx <= 0) return "/";
        return n[..idx];
    }

    public Stream Open(string path)
    {
        if (!_files.TryGetValue(Normalize(path), out var entry))
            throw new FileNotFoundException(path);
        return entry.Open();
    }

    public byte[] ReadAllBytes(string path)
    {
        using var s = Open(path);
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public string ReadAllText(string path) => ReadAllText(path, Encoding.UTF8);

    public string ReadAllText(string path, Encoding encoding)
    {
        var bytes = ReadAllBytes(path);
        int offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        return encoding.GetString(bytes, offset, bytes.Length - offset);
    }

    public IEnumerable<string> EnumerateFilesWithExtension(string extension)
    {
        foreach (var f in _files.Keys)
        {
            if (f.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                yield return f;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _zip.Dispose();
        _stream.Dispose();
    }

    private void AddDir(string dir)
    {
        if (string.IsNullOrEmpty(dir)) dir = "/";
        if (!dir.StartsWith('/')) dir = "/" + dir;
        _dirs.Add(dir.TrimEnd('/') is { Length: 0 } ? "/" : dir.TrimEnd('/'));
    }

    private static string NormalizeEntryPath(string fullName)
    {
        string n = fullName.Replace('\\', '/').Trim();
        while (n.StartsWith("./", StringComparison.Ordinal)) n = n[2..];
        if (!n.StartsWith('/')) n = "/" + n;
        while (n.Contains("//", StringComparison.Ordinal))
            n = n.Replace("//", "/", StringComparison.Ordinal);
        return n.TrimEnd('/');
    }

    private static string Normalize(string path) => NormalizeEntryPath(path);
}
