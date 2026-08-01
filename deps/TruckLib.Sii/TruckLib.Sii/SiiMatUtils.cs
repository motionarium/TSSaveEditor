using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TruckLib.Sii
{
    internal partial class SiiMatUtils
    {
        public static string RemoveComments(string sii) 
        {
            var sb = new StringBuilder();

            int i;
            for (i = 0; i < sii.Length; i++)
            {
                char c = sii[i];

                // Skip quoted strings
                if (c == '"')
                {
                    sb.Append(c);
                    i++;
                    for (; i < sii.Length - 1 && sii[i] != '"'; i++)
                    {
                        sb.Append(sii[i]);
                    }
                    sb.Append(c);
                }
                // Single-line comment with #
                else if (c == '#')
                {
                    SkipUntil('\n');
                    if (sii[i - 1] == '\r')
                        sb.Append('\r');
                    sb.Append('\n');
                }
                else if (c == '/')
                {
                    // Single-line comment with //
                    if (Peek('/'))
                    {
                        SkipUntil('\n');
                        if (sii[i - 1] == '\r')
                            sb.Append('\r');
                        sb.Append('\n');
                    }
                    // C-style multi-line comment
                    else if (Peek('*'))
                    {
                        SkipUntilStr("*/");
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    sb.Append(c);
                }                    
            }

            return sb.ToString();

            bool Peek(char c)
            {
                return i < sii.Length - 1 && sii[i + 1] == c;
            }

            void SkipUntil(char c)
            {
                for (; i < sii.Length - 1 && sii[i] != c; i++);
            }

            void SkipUntilStr(string s)
            {
                for (; i < sii.Length - 1; i++)
                {
                    if (Equals(s))
                    {
                        i += s.Length - 1;
                        return;
                    }
                }
            }

            bool Equals(string s)
            {
                for (int j = 0; j < s.Length; j++)
                {
                    if (s[j] != sii[i + j]) return false;
                }
                return true;
            }
        }

        internal static void AddAttribute(Unit unit, string name, dynamic value, bool overrideOnDuplicate)
        {
            if (unit.Attributes.ContainsKey(name) && overrideOnDuplicate)
            {
                unit.Attributes[name] = value;
            }
            else
            {
                unit.Attributes.Add(name, value);
            }
        }

        [GeneratedRegex(@"^(.+)\[(.*)\]$")]
        internal static partial Regex ListOrArrayAttributePattern();

        internal static void ParseListOrArrayAttribute(Unit unit, string name, dynamic value,
            Dictionary<string, int> arrInsertIndex, bool overrideOnDuplicate)
        {
            var match = ListOrArrayAttributePattern().Match(name);
            if (!match.Success)
                throw new ArgumentException("Not an array entry attribute", nameof(name));

            var arrName = match.Groups[1].Value;
            var hasExplicitIndex = int.TryParse(match.Groups[2].Value, out int explicitIndex);
            arrInsertIndex.TryAdd(arrName, 0);

            // figure out if this is a fixed-length array entry or a list entry
            // and create the thing if it doesn't exist yet
            bool isFixedLengthArray;
            if (unit.Attributes.TryGetValue(arrName, out var whatsAllThisThen))
            {
                isFixedLengthArray = whatsAllThisThen is int or Array;
            }
            else
            {
                isFixedLengthArray = hasExplicitIndex;
                if (isFixedLengthArray)
                {
                    int initSize = explicitIndex + 1;
                    var arr = new dynamic[initSize];
                    AddAttribute(unit, arrName, arr, overrideOnDuplicate);
                }
                else
                {
                    AddAttribute(unit, arrName, new List<dynamic>(), overrideOnDuplicate);
                }
            }

            // insert the value
            if (isFixedLengthArray)
            {
                var val = unit.Attributes[arrName];
                dynamic[] arr;
                if (val is int)
                {
                    // existing val is int => it's a fixed-length array
                    // where the length has been read in, and now we need to
                    // create the actual array
                    arr = new dynamic[val];
                    unit.Attributes[arrName] = arr;
                }
                else
                {
                    arr = val;
                }

                if (hasExplicitIndex)
                {
                    if (arr.Length - 1 < explicitIndex)
                    {
                        Array.Resize(ref arr, explicitIndex + 1);
                        unit.Attributes[arrName] = arr;
                    }
                    arr[explicitIndex] = value;
                    arrInsertIndex[arrName] = explicitIndex + 1;
                }
                else
                {
                    var idx = arrInsertIndex[arrName];
                    if (arr.Length - 1 < idx)
                    {
                        Array.Resize(ref arr, idx + 1);
                        unit.Attributes[arrName] = arr;
                    }
                    arr[idx] = value;
                    arrInsertIndex[arrName] += 1;
                }
            }
            else
            {
                // handle weird edge case where an array is accidentally
                // defined like this:
                //     foo: "bar"
                //     ... other stuff ...
                //     foo[1]: "baz"
                if (unit.Attributes[arrName] is not List<dynamic> or Array)
                {
                    var old = unit.Attributes[arrName];
                    unit.Attributes[arrName] = new dynamic[] { old };
                    ParseListOrArrayAttribute(unit, name, value, arrInsertIndex, overrideOnDuplicate);
                }
                else
                {
                    unit.Attributes[arrName].Add(value);
                }
            }
        }
    }
}
