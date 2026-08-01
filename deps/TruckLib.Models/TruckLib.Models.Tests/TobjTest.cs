using System;
using System.Collections.Generic;
using System.Text;
using TruckLib.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TruckLib.Models.Tests
{
    public class TobjTest
    {
        byte[] tobjData;

        public TobjTest()
        {
            tobjData = File.ReadAllBytes("Data/TobjTest/cubetx.tobj");
        }

        [Fact]
        public void Deserialize()
        {
            var tobj = Tobj.Load(tobjData);

            Assert.Equal(TobjColorSpace.Srgb, tobj.ColorSpace);
            Assert.True(tobj.Compress);
            Assert.Equal(TobjFilter.Default, tobj.MagFilter);
            Assert.Equal(TobjFilter.Default, tobj.MinFilter);
            Assert.Equal(TobjMipFilter.Default, tobj.MipFilter);
            Assert.Equal("/model/simple_cube/cubetx.dds", tobj.TexturePath);
            Assert.Equal(TobjType.Map2D, tobj.Type);
        }

        [Fact]
        public void Serialize()
        {
            var tobj = Tobj.Load(tobjData);

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);
            tobj.Serialize(w);

            Assert.Equal(ms.ToArray(), tobjData);
        }

        [Fact]
        public void KeepOpenTrueRespected()
        {
            var ms = new MemoryStream(tobjData);
            var tobj = Tobj.Load(ms, true);
            Assert.True(ms.CanRead);
        }

        [Fact]
        public void KeepOpenFalseRespected()
        {
            var ms = new MemoryStream(tobjData);
            var tobj = Tobj.Load(ms, false);
            Assert.False(ms.CanRead);
        }
    }
}
