using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib
{
    public class DiskFileSystem : IFileSystem
    {
        /// <inheritdoc/>
        public char DirectorySeparator => Path.DirectorySeparatorChar;

        /// <inheritdoc/>
        public bool FileExists(string path) => File.Exists(path);

        /// <inheritdoc/>
        public bool DirectoryExists(string path) => Directory.Exists(path);

        /// <inheritdoc/>
        public IList<string> GetFiles(string path) => Directory.GetFiles(path);

        /// <inheritdoc/>
        public string GetParent(string path) => Directory.GetParent(path).FullName;

        /// <inheritdoc/>
        public Stream Open(string path) => File.OpenRead(path);

        /// <inheritdoc/>
        public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);

        /// <inheritdoc/>
        public string ReadAllText(string path) => File.ReadAllText(path);

        /// <inheritdoc/>
        public string ReadAllText(string path, Encoding encoding) => File.ReadAllText(path, encoding);
    }
}
