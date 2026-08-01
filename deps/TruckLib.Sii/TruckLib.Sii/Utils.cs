using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.Sii
{
    internal static class Utils
    {
        public static string TrimByteOrderMark(string s)
        {
            if (s.StartsWith('\uFEFF'))
            {
                s = s.Trim(['\uFEFF']);
            }
            return s;
        }
    }
}
