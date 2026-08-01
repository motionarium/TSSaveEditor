using System;
using System.IO;
using TruckLib;
using TruckLib.HashFs;

namespace Ets2SaveEditor.Core;

/// <summary>Opens HashFS or ZIP-based .scs as an <see cref="IFileSystem"/>.</summary>
public sealed class ScsArchive : IDisposable
{
    public IFileSystem FileSystem { get; }
    public bool IsZip { get; }
    private readonly IDisposable? _owned;

    private ScsArchive(IFileSystem fs, bool isZip, IDisposable? owned)
    {
        FileSystem = fs;
        IsZip = isZip;
        _owned = owned;
    }

    public static ScsArchive Open(string path)
    {
        if (ZipScsFileSystem.IsZipArchive(path))
        {
            var zip = new ZipScsFileSystem(path);
            return new ScsArchive(zip, isZip: true, zip);
        }

        IHashFsReader reader = HashFsReader.Open(path);
        return new ScsArchive(reader, isZip: false, reader);
    }

    public void Dispose() => _owned?.Dispose();
}
