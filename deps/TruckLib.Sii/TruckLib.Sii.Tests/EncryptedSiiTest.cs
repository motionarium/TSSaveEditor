using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.Sii.Tests
{
    public class EncryptedSiiTest
    {
        readonly byte[] unencrypted;
        readonly byte[] encrypted;

        public EncryptedSiiTest()
        {
            unencrypted = File.ReadAllBytes("Data/unencrypted.sii");
            encrypted = File.ReadAllBytes("Data/encrypted.sii");
        }

        [Fact]
        public void Decrypt()
        {
            var actual = EncryptedSii.Decrypt(encrypted);
            Assert.Equal(unencrypted, actual);
        }
    }
}
