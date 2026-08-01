using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.Sii.Tests
{
    public class ThreeNKTest
    {
        [Fact]
        public void Encode()
        {
            var expected = Convert.FromHexString("336E4B010048C7EB95AB7C0A7F5C4128");
            var actual = ThreeNK.Encode(Encoding.ASCII.GetBytes("727 (wysi)"), 72);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Decode()
        {
            var expected = Encoding.ASCII.GetBytes("727 (wysi)");
            var actual = ThreeNK.Decode(Convert.FromHexString("336E4B010048C7EB95AB7C0A7F5C4128"));
            Assert.Equal(expected, actual);
        }
    }
}
