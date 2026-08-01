using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.Models.Ppd
{
    internal struct TerrainPointVariant : IBinarySerializable
    {
        public uint Start { get; set; }

        public uint Length { get; set; }

        public TerrainPointVariant() { }

        public TerrainPointVariant(uint start, uint length)
        {
            Start = start;
            Length = length;
        }

        public void Deserialize(BinaryReader r, uint? version = null)
        {
            Start = r.ReadUInt32();
            Length = r.ReadUInt32();
        }

        public void Serialize(BinaryWriter w)
        {
            w.Write(Start);
            w.Write(Length);
        }
    }
}
