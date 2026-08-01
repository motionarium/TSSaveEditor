using Sprache;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TruckLib.Sii
{
    /// <summary>
    /// De/serializes a SII file.
    /// </summary>
    internal static class SiiParser
    {
        private const string IncludeKeyword = "@include";

        /// <summary>
        /// Sets how to handle duplicate attributes in a unit.
        /// </summary>
        private static readonly bool OverrideOnDuplicate = true;

        public static SiiFile DeserializeFromString(string sii, string siiPath = "", 
            bool ignoreMissingIncludes = false)
        {
            return DeserializeFromString(sii, siiPath, new DiskFileSystem(), ignoreMissingIncludes);
        }

        public static SiiFile DeserializeFromString(string sii, string siiPath, IFileSystem fs,
            bool ignoreMissingIncludes)
        {
            sii = Utils.TrimByteOrderMark(sii);

            var siiFile = new SiiFile();

            sii = SiiMatUtils.RemoveComments(sii);
            (sii, siiFile.Includes) = InsertIncludes(sii, siiPath, fs, ignoreMissingIncludes);

            var firstPassUnits = ParserElements.Sii.Parse(sii);
            foreach (var firstPassUnit in firstPassUnits)
            {
                siiFile.Units.Add(SecondPass(firstPassUnit, OverrideOnDuplicate));
            }

            return siiFile;
        }

        private static (string sii, HashSet<string> includes) InsertIncludes(string sii, string siiDir, 
            IFileSystem fs, bool ignoreMissingIncludes, HashSet<string> parents = null)
        {
            var output = new StringBuilder();
            var includes = new HashSet<string>();

            parents ??= [];

            using var reader = new StringReader(sii);
            string line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (!line.StartsWith(IncludeKeyword))
                {
                    output.AppendLine(line);
                }
                else
                {
                    var match = Regex.Match(line, @"@include ""(.*)""");
                    if (match.Groups.Count < 1)
                    {
                        continue;
                    }
                    var suiPath = match.Groups[1].Value;
                    
                    // make relative paths absolute.
                    // the check is necessary because absolute paths in @include directives
                    // are not guaranteed to start with a slash
                    if (!fs.FileExists(suiPath))
                    {
                        suiPath = siiDir + "/" + suiPath;
                    }
                    includes.Add(suiPath);

                    if (parents.Contains(suiPath))
                    {
                        // Disallow recursive includes
                        continue;
                    }

                    if (!fs.FileExists(suiPath))
                    {
                        if (ignoreMissingIncludes)
                        {
                            continue;
                        }
                        else
                        {
                            throw new FileNotFoundException("Included file was not found.", suiPath);
                        }
                    }
                    var fileContents = fs.ReadAllText(suiPath);
                    fileContents = Utils.TrimByteOrderMark(fileContents);
                    fileContents = SiiMatUtils.RemoveComments(fileContents);

                    var lastSlash = suiPath.LastIndexOf('/');
                    string suiDir = lastSlash != -1 
                        ? suiPath[0..lastSlash] 
                        : siiDir;
                    (fileContents, var innerIncludes) = InsertIncludes(
                        fileContents, suiDir, fs, ignoreMissingIncludes, 
                        [.. parents, suiPath]);
                    includes.UnionWith(innerIncludes);
                    output.AppendLine(fileContents);
                }
            }

            return (output.ToString(), includes);
        }

        private static Unit SecondPass(FirstPassUnit firstPass, bool overrideOnDuplicate)
        {
            Dictionary<string, int> arrInsertIndex = [];
            var secondPass = new Unit(firstPass.ClassName, firstPass.UnitName);
            foreach (var (key, value) in firstPass.Attributes)
            {
                if (key.EndsWith(']'))
                {
                    SiiMatUtils.ParseListOrArrayAttribute(secondPass, key, value, arrInsertIndex, overrideOnDuplicate);
                }
                else
                {
                    SiiMatUtils.AddAttribute(secondPass, key, value, overrideOnDuplicate);
                }
            }
            return secondPass;
        }

        public static void Save(SiiFile siiFile, string path, string indentation = "\t")
        {
            var str = Serialize(siiFile, indentation);
            File.WriteAllText(path, str);
        }

        public static string Serialize(SiiFile siiFile, string indentation = "\t")
        {
            var sb = new StringBuilder();

            sb.AppendLine(ParserElements.SiiHeader);
            sb.AppendLine("{\n");

            foreach (var unit in siiFile.Units)
            {
                sb.AppendLine($"{unit.Class} : {unit.Name}\n{{");
                ParserElements.SerializeAttributes(sb, unit.Attributes, indentation);
                sb.AppendLine("}\n");
            }

            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
