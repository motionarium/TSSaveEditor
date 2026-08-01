using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.Sii
{
    /// <summary>
    /// Functions for decrypting SII files.
    /// </summary>
    public static class EncryptedSii
    {
        private static readonly byte[] siiKey = [
            0x2a, 0x5f, 0xcb, 0x17, 0x91, 0xd2, 0x2f, 0xb6,
            0x02, 0x45, 0xb3, 0xd8, 0x36, 0x9e, 0xd0, 0xb2,
            0xc2, 0x73, 0x71, 0x56, 0x3f, 0xbf, 0x1f, 0x3c,
            0x9e, 0xdf, 0x6b, 0x11, 0x82, 0x5a, 0x5d, 0x0a
            ];

        /// <summary>
        /// Decrypts a SII file.
        /// </summary>
        /// <param name="sii">The buffer containing the encrypted SII file.</param>
        /// <returns>The decrypted SII file.</returns>
        public static byte[] Decrypt(byte[] sii)
        {
            using var ms = new MemoryStream(sii);
            return Decrypt(ms);
        }

        /// <summary>
        /// Decrypts a SII file.
        /// </summary>
        /// <param name="sii">The stream containing the encrypted SII file.</param>
        /// <returns>The decrypted SII file.</returns>
        public static byte[] Decrypt(Stream sii)
        {
            using var r = new BinaryReader(sii);

            var header = new EncryptedSiiHeader();
            header.Deserialize(r);

            var remaining = (int)(r.BaseStream.Length - r.BaseStream.Position);
            var ciphertext = r.ReadBytes(remaining);

            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = siiKey;
            var plaintext = aes.DecryptCbc(ciphertext, header.IV);

            var decrypted = Decompress(plaintext);
            return decrypted;
        }

        private static byte[] Decompress(byte[] input)
        {
            using var inMs = new MemoryStream(input);
            using var zlibStream = new ZLibStream(inMs, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            zlibStream.CopyTo(outMs);
            var decompressed = outMs.ToArray();
            return decompressed;
        }
    }
}
