using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.Core.Tests
{
    public class IOExtensionsTest
    {
        [Fact]
        public void ReadToken()
        {
            var input = Convert.FromHexString("E402000000000000");
            using var ms = new MemoryStream(input);
            using var r = new BinaryReader(ms);
            var actual = r.ReadToken();
            Assert.Equal(new Token("hi"), actual);
        }

        [Fact]
        public void WriteToken()
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(new Token("hi"));
            var expected = Convert.FromHexString("E402000000000000");
            Assert.Equal(expected, ms.ToArray());
        }

        [Fact]
        public void ReadVector2()
        {
            var input = Convert.FromHexString("0000003F0000003F");
            using var ms = new MemoryStream(input);
            using var r = new BinaryReader(ms);
            var actual = r.ReadVector2();
            Assert.Equal(new Vector2(0.5f, 0.5f), actual);
        }

        [Fact]
        public void WriteVector2()
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(new Vector2(0.5f, 0.5f));
            var expected = Convert.FromHexString("0000003F0000003F");
            Assert.Equal(expected, ms.ToArray());
        }

        [Fact]
        public void ReadVector3()
        {
            var input = Convert.FromHexString("0000003F0000003F0000003F");
            using var ms = new MemoryStream(input);
            using var r = new BinaryReader(ms);
            var actual = r.ReadVector3();
            Assert.Equal(new Vector3(0.5f, 0.5f, 0.5f), actual);
        }

        [Fact]
        public void WriteVector3()
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(new Vector3(0.5f, 0.5f, 0.5f));
            var expected = Convert.FromHexString("0000003F0000003F0000003F");
            Assert.Equal(expected, ms.ToArray());
        }

        [Fact]
        public void ReadVector4()
        {
            var input = Convert.FromHexString("0000003F0000003F0000003F0000003F");
            using var ms = new MemoryStream(input);
            using var r = new BinaryReader(ms);
            var actual = r.ReadVector4();
            Assert.Equal(new Vector4(0.5f, 0.5f, 0.5f, 0.5f), actual);
        }

        [Fact]
        public void WriteVector4()
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(new Vector4(0.5f, 0.5f, 0.5f, 0.5f));
            var expected = Convert.FromHexString("0000003F0000003F0000003F0000003F");
            Assert.Equal(expected, ms.ToArray());
        }

        [Fact]
        public void ReadQuaternion()
        {
            var input = Convert.FromHexString("000020400000003F0000003F0000003F");
            using var ms = new MemoryStream(input);
            using var r = new BinaryReader(ms);
            var actual = r.ReadQuaternion();
            Assert.Equal(new Quaternion(0.5f, 0.5f, 0.5f, 2.5f), actual);
        }

        [Fact]
        public void WriteQuaternion()
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(new Quaternion(0.5f, 0.5f, 0.5f, 2.5f));
            var expected = Convert.FromHexString("000020400000003F0000003F0000003F");
            Assert.Equal(expected, ms.ToArray());
        }

        [Fact]
        public void ReadPascalString()
        {
            var input = Convert.FromHexString("0B000000000000006BC3A47365666F6E647565");
            using var ms = new MemoryStream(input);
            using var r = new BinaryReader(ms);
            var actual = r.ReadPascalString();
            Assert.Equal("käsefondue", actual);
        }

        [Fact]
        public void WritePascalString()
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.WritePascalString("käsefondue");
            var expected = Convert.FromHexString("0B000000000000006BC3A47365666F6E647565");
            Assert.Equal(expected, ms.ToArray());
        }

        [Fact]
        public void ReadColor()
        {
            var input = Convert.FromHexString("11223344");
            using var ms = new MemoryStream(input);
            using var r = new BinaryReader(ms);
            var actual = r.ReadColor();
            Assert.Equal(Color.FromArgb(0x44, 0x11, 0x22, 0x33), actual);
        }

        [Fact]
        public void WriteColor()
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            w.Write(Color.FromArgb(0x44, 0x11, 0x22, 0x33));
            var expected = Convert.FromHexString("11223344");
            Assert.Equal(expected, ms.ToArray());
        }

        [Fact]
        public void ReadShortList()
        {
            var input = Convert.FromHexString("aa00bb00cc00");
            using var ms = new MemoryStream(input);
            using var r = new BinaryReader(ms);
            var actual = r.ReadObjectList<short>(3);
            Assert.Equal([0xaa, 0xbb, 0xcc], actual);
        }

        [Fact]
        public void WriteLimitedList()
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            var list = new LimitedList<short>(4) { 0xaa, 0xbb, 0xcc };
            w.WriteObjectList(list);
            var expected = Convert.FromHexString("aa00bb00cc00");
            Assert.Equal(expected, ms.ToArray());
        }
    }
}
