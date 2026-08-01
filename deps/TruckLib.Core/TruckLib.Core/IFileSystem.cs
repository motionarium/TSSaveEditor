using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib
{
    /// <summary>
    /// Interface which abstracts read-only file system access to enable methods to read from
    /// either disk or HashFS.
    /// </summary>
    public interface IFileSystem
    {
        /// <summary>
        /// The character used to separate directory levels in a path string.
        /// </summary>
        char DirectorySeparator { get; }

        /// <summary>
        /// Determines whether the specified file exists.
        /// </summary>
        /// <param name="path">The file to check.</param>
        /// <returns><c>true</c> if the caller has the required permissions and <c>path</c>
        /// contains the name of an existing file; otherwise, <c>false</c>.</returns>
        bool FileExists(string path);

        /// <summary>
        /// Determines whether the given path refers to an existing directory.    
        /// </summary>
        /// <param name="path">The directory to check.</param>
        /// <returns><c>true</c> if path refers to an existing directory; <c>false</c> if 
        /// the directory does not exist or an error occurs when trying to determine 
        /// if the specified directory exists.</returns>
        bool DirectoryExists(string path);

        /// <summary>
        /// Returns the names of files (including their paths) in the specified directory.
        /// </summary>
        /// <param name="path">The absolute path to the directory to search.</param>
        /// <returns>An array of the full names (including paths) for the files in the
        /// specified directory, or an empty array if no files are found.</returns>
        IList<string> GetFiles(string path);

        /// <summary>
        /// Opens a binary file, reads the contents of the file into a byte array,
        /// and then closes the file.
        /// </summary>
        /// <param name="path">The file to open for reading.</param>
        /// <returns>A byte array containing the contents of the file.</returns>
        byte[] ReadAllBytes(string path);

        /// <summary>
        /// Opens a text file, reads all the text in the file into a string, and then
        /// closes the file.
        /// </summary>
        /// <param name="path">The file to open for reading.</param>
        /// <returns>A string containing all the text in the file.</returns>
        string ReadAllText(string path);

        /// <summary>
        /// Opens a file, reads all text in the file with the specified encoding, and then
        /// closes the file.
        /// </summary>
        /// <param name="path">The file to open for reading.</param>
        /// <param name="encoding">The encoding applied to the contents of the file.</param>
        /// <returns></returns>
        string ReadAllText(string path, Encoding encoding);

        /// <summary>
        /// Opens a stream on the specified path.
        /// </summary>
        /// <param name="path">The file to open.</param>
        /// <returns>A stream for reading the file.</returns>
        Stream Open(string path);

        /// <summary>
        /// Retrieves the parent directory of the specified path.
        /// </summary>
        /// <param name="path">The path for which to retrieve the parent directory.</param>
        /// <returns>The parent directory, or <c>null</c> if path is the root directory.</returns>
        string GetParent(string path);
    }
}
