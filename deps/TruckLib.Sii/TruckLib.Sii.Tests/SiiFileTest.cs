using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.Sii.Tests
{
    public class SiiFileTest
    {
        [Fact]
        public void LoadThrowsIfTooShort()
        {
            byte[] bytes = [1, 2, 3];
            Assert.Throws<ArgumentException>(() => SiiFile.Load(bytes));
        }

        [Fact]
        public void DecodeDoesNotThrowIfTooShort()
        {
            byte[] bytes = [1, 2, 3];
            Assert.Equal(bytes, SiiFile.Decode(bytes));
        }
    }
}
