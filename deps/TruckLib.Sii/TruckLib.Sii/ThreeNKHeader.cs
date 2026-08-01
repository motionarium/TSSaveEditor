using System.IO;
using System.Linq;

namespace TruckLib.Sii
{
    internal struct ThreeNKHeader : IBinarySerializable
    {
        public static readonly char[] Magic = ['3', 'n', 'K'];
        public ushort VersionMaybe = 1;
        public byte Seed;

        public ThreeNKHeader()
        {
        }

        public void Deserialize(BinaryReader r, uint? version = null)
        {
            var magic = r.ReadChars(3);
            if (!Enumerable.SequenceEqual(magic, Magic))
            {
                throw new InvalidDataException("Not a 3nK-encoded file.");
            }
            VersionMaybe = r.ReadUInt16();
            Seed = r.ReadByte();
        }

        public void Serialize(BinaryWriter w)
        {
            w.Write(Magic);
            w.Write(VersionMaybe);
            w.Write(Seed);
        }
    }
}
