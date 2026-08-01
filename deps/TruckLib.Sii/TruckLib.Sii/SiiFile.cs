using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TruckLib.Sii
{
    /// <summary>
    /// Represents a SII file.
    /// </summary>
    public class SiiFile
    {
        // https://modding.scssoft.com/wiki/Documentation/Engine/Units

        /// <summary>
        /// Units in this file.
        /// </summary>
        public List<Unit> Units { get; set; } = [];

        /// <summary>
        /// Instantiates an empty SII file.
        /// </summary>
        public SiiFile() { }

        /// <summary>
        /// The paths of files which were referenced by a <c>@include</c> directive.
        /// </summary>
        public HashSet<string> Includes { get; internal set; }

        /// <summary>
        /// Deserializes a SII file.
        /// </summary>
        /// <param name="sii">The string containing the SII file.</param>
        /// <param name="siiDirectory">The path of the directory in which the SII file is located.
        /// Required for inserting <c>@include</c>s. Can be omitted if the file is known not to
        /// have <c>@include</c>s.</param>
        /// <returns>A SiiFile object.</returns>
        public static SiiFile Load(string sii, string siiDirectory = "") =>
            Load(sii, siiDirectory, new DiskFileSystem());

        /// <summary>
        /// Deserializes a SII file.
        /// </summary>
        /// <param name="sii">The string containing the SII file.</param>
        /// <param name="siiDirectory">The path of the directory in which the SII file is located.
        /// Required for inserting <c>@include</c>s. Can be omitted if the file is known not to
        /// have <c>@include</c>s.</param>
        /// <param name="fs">The file system to load <c>@include</c>d files from.</param>
        /// <returns>A <see>SiiFile</see> object.</returns>
        public static SiiFile Load(string sii, string siiDirectory, IFileSystem fs) =>
            Load(sii, siiDirectory, fs, false);

        /// <summary>
        /// Deserializes a SII file.
        /// </summary>
        /// <param name="sii">The string containing the SII file.</param>
        /// <param name="siiDirectory">The path of the directory in which the SII file is located.
        /// Required for inserting <c>@include</c>s. Can be omitted if the file is known not to
        /// have <c>@include</c>s.</param>
        /// <param name="fs">The file system to load <c>@include</c>d files from.</param>
        /// <param name="ignoreMissingIncludes">If true, missing <c>@include</c>d files are ignored.
        /// If false, an exception will be thrown.</param>
        /// <returns>A <see>SiiFile</see> object.</returns>
        public static SiiFile Load(string sii, string siiDirectory, IFileSystem fs, bool ignoreMissingIncludes) =>
            SiiParser.DeserializeFromString(sii, siiDirectory, fs, ignoreMissingIncludes);

        /// <summary>
        /// Deserializes a SII file.
        /// </summary>
        /// <param name="sii">The buffer containing the SII file.</param>
        /// <param name="siiDirectory">The path of the directory in which the SII file is located.
        /// Required for inserting <c>@include</c>s. Can be omitted if the file is known not to
        /// have <c>@include</c>s.</param>
        /// <returns>A <see>SiiFile</see> object.</returns>
        public static SiiFile Load(byte[] sii, string siiDirectory = "") =>
            Load(sii, siiDirectory, new DiskFileSystem());

        /// <summary>
        /// Deserializes a SII file.
        /// </summary>
        /// <param name="sii">The buffer containing the SII file.</param>
        /// <param name="siiDirectory">The path of the directory in which the SII file is located.
        /// Required for inserting <c>@include</c>s. Can be omitted if the file is known not to
        /// have <c>@include</c>s.</param>
        /// <param name="fs">The file system to load <c>@include</c>d files from.</param>
        /// <returns>A <see>SiiFile</see> object.</returns>
        public static SiiFile Load(byte[] sii, string siiDirectory, IFileSystem fs) =>
            Load(sii, siiDirectory, fs, false);

        /// <summary>
        /// Deserializes a SII file.
        /// </summary>
        /// <param name="sii">The buffer containing the SII file.</param>
        /// <param name="siiDirectory">The path of the directory in which the SII file is located.
        /// Required for inserting <c>@include</c>s. Can be omitted if the file is known not to
        /// have <c>@include</c>s.</param>
        /// <param name="fs">The file system to load <c>@include</c>d files from.</param>
        /// <param name="ignoreMissingIncludes">If true, missing <c>@include</c>d files are ignored.
        /// If false, an exception will be thrown.</param>
        /// <returns>A <see>SiiFile</see> object.</returns>
        public static SiiFile Load(byte[] sii, string siiDirectory, IFileSystem fs, 
            bool ignoreMissingIncludes)
        {
            if (sii.Length < 4)
                throw new ArgumentException("Too short to be a valid SII file", nameof(sii));

            var magic = Encoding.ASCII.GetString(sii[0..4]);
            if (magic == "ScsC")
            {
                var decrypted = EncryptedSii.Decrypt(sii);
                return Load(decrypted, siiDirectory, fs, ignoreMissingIncludes);
            }
            else if (magic.StartsWith("3nK"))
            {
                var decoded = ThreeNK.Decode(sii);
                return Load(decoded, siiDirectory, fs, ignoreMissingIncludes);
            }
            else
            {
                return SiiParser.DeserializeFromString(Encoding.UTF8.GetString(sii), 
                    siiDirectory, fs, ignoreMissingIncludes);
            }
        }

        /// <summary>
        /// Opens a SII file.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>A SiiFile object.</returns>
        public static SiiFile Open(string path) =>
            Open(path, new DiskFileSystem());

        /// <summary>
        /// Opens a SII file.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="fs">The file system to load this file and <c>@include</c>d files from.</param>
        /// <returns>A <see>SiiFile</see> object.</returns>
        public static SiiFile Open(string path, IFileSystem fs)
        {
            var file = fs.ReadAllBytes(path);
            var siiDirectory = fs.GetParent(path);
            return Load(file, siiDirectory, fs);
        }

        /// <summary>
        /// Opens a SII file.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="fs">The file system to load this file and <c>@include</c>d files from.</param>
        /// <param name="ignoreMissingIncludes">If true, missing <c>@include</c>d files are ignored.
        /// If false, an exception will be thrown.</param>
        /// <returns>A <see>SiiFile</see> object.</returns>
        public static SiiFile Open(string path, IFileSystem fs, bool ignoreMissingIncludes)
        {
            var file = fs.ReadAllBytes(path);
            var siiDirectory = fs.GetParent(path);
            return Load(file, siiDirectory, fs, ignoreMissingIncludes);
        }

        /// <summary>
        /// Decodes a 3nK-encoded or encrypted SII file to its regular text form.
        /// </summary>
        /// <param name="sii">The SII file to decode.</param>
        /// <returns>The decoded SII file. If the input was already in text form
        /// or is unsupported, it is returned unchanged.</returns>
        public static byte[] Decode(byte[] sii)
        {
            if (sii.Length < 4)
                return sii;

            var magic = Encoding.ASCII.GetString(sii[0..4]);
            if (magic == "ScsC")
            {
                var decrypted = EncryptedSii.Decrypt(sii);
                return Decode(decrypted);
            }
            else if (magic.StartsWith("3nK"))
            {
                var decoded = ThreeNK.Decode(sii);
                return Decode(decoded);
            }
            else
            {
                return sii;
            }
        }

        /// <summary>
        /// Serializes this object to a string.
        /// </summary>
        /// <param name="indentation">The string used as indentation inside units.</param>
        public string Serialize(string indentation = "\t") =>
            SiiParser.Serialize(this, indentation);

        /// <summary>
        /// Serializes this object and writes it to a file.
        /// </summary>
        /// <param name="path">The output path.</param>
        /// <param name="indentation">The string used as indentation inside units.</param>
        public void Save(string path, string indentation = "\t") =>
            SiiParser.Save(this, path, indentation);
    }
}
