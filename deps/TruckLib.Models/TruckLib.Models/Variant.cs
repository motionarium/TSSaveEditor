using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace TruckLib.Models
{
    [DebuggerDisplay("{Name.String, nq}")]
    public class Variant
    {
        public Token Name { get; set; }

        public List<PartAttribute> Attributes { get; set; } = [];

        public Variant(Token name)
        {
            Name = name;
        }
    }
}
