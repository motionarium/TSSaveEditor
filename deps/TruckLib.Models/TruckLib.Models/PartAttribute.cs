using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace TruckLib.Models
{
    [DebuggerDisplay("{Tag.String, nq}: {Value}")]
    public class PartAttribute
    {
        public int Type { get; set; }

        public Token Tag { get; set; }

        public uint Value { get; set; }
    }
}
