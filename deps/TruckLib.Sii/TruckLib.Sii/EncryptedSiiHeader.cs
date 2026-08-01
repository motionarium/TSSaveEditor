using System;
using System.IO;
using System.Linq;

namespace TruckLib.Sii
{
    internal struct EncryptedSiiHeader : IBinarySerializable
    {
        public static readonly char[] Magic = ['S', 'c', 's', 'C'];
        public byte[] Hmac;
        public byte[] IV;
        public uint DataSize;

        public void Deserialize(BinaryReader r, uint? version = null)
        {
            var magic = r.ReadChars(4);
            if (!Enumerable.SequenceEqual(magic, Magic))
            {
                throw new InvalidDataException("Not an encrypted SII file.");
            }

            Hmac = r.ReadBytes(32);
            IV = r.ReadBytes(16);
            DataSize = r.ReadUInt32();
        }

        public void Serialize(BinaryWriter w)
        {
            w.Write(Magic);
            w.Write(Hmac);
            w.Write(IV);
            w.Write(DataSize);
        }
    }
}
