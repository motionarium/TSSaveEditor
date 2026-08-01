using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace TruckLib.Models
{
    [DebuggerDisplay("{Name.String, nq}")]
    public class Look
    {
        public Token Name { get; set; }

        public List<string> Materials { get; set; } = [];

        public Look()
        {
        }

        public Look(Token name)
        {
            Name = name;
        }
    }
}
