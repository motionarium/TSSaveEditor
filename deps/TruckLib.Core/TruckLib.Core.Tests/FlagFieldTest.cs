using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TruckLib;

namespace TruckLib.Core.Tests
{
    public class FlagFieldTest
    {
        [Fact]
        public void Get() 
        {
            var ff = new FlagField(0b100);
            Assert.True(ff[2]);
            Assert.False(ff[1]);
        }

        [Fact]
        public void GetThrowsOutOfRangeException()
        {
            Assert.Throws<IndexOutOfRangeException>(() =>
            {
                var ff = new FlagField(0b100);
                _ = ff[32];
            });
        }

        [Fact]
        public void Set()
        {
            var ff = new FlagField(0b100);
            ff[3] = true;
            Assert.Equal(0b1100u, ff.Bits);
            ff[3] = false;
            Assert.Equal(0b0100u, ff.Bits);
        }

        [Fact]
        public void SetThrowsOutOfRangeException()
        {
            Assert.Throws<IndexOutOfRangeException>(() =>
            {
                var ff = new FlagField();
                ff[32] = true;
            });
        }

        [Fact]
        public void GetByte()
        {
            var ff = new FlagField(0xdeadbeef);
            Assert.Equal((byte)0xad, ff.GetByte(2));
        }

        [Fact]
        public void GetByteThrowsOutOfRangeException()
        {
            Assert.Throws<IndexOutOfRangeException>(() =>
            {
                var ff = new FlagField(0b100);
                _ = ff.GetByte(4);
            });
        }

        [Fact]
        public void SetByte()
        {
            var ff = new FlagField(0x11220044);
            ff.SetByte(1, 0x33);
            Assert.Equal(0x11223344u, ff.Bits);
        }

        [Fact]
        public void SetByteThrowsOutOfRangeException()
        {
            Assert.Throws<IndexOutOfRangeException>(() =>
            {
                var ff = new FlagField();
                ff.SetByte(4, 0);
            });
        }

        [Fact]
        public void ToBoolArray()
        {
            var ff = new FlagField(0b100);
            var arr = ff.ToBoolArray();
            Assert.Equal(32, arr.Length);
            Assert.False(arr[1]);
            Assert.True(arr[2]);
        }

        [Fact]
        public void GetBitSring()
        {
            var ff = new FlagField(0b110110);
            Assert.Equal((uint)0b11011, ff.GetBitString(1, 5));
        }

        [Fact]
        public void GetBitStringWithZeroLength()
        {
            var ff = new FlagField(0b110110);
            Assert.Equal(0U, ff.GetBitString(1, 0));
        }

        [Fact]
        public void GetBitStringThrowsOutOfRangeException()
        {
            Assert.Throws<IndexOutOfRangeException>(() =>
            {
                var ff = new FlagField();
                ff.GetBitString(28, 10);
            });
        }

        [Fact]
        public void SetBitString()
        {
            var ff = new FlagField();
            ff.SetBitString(1, 5, 0b11011);
            Assert.Equal((uint)0b110110, ff.Bits);
        }

        [Fact]
        public void SetBitStringWithZeroLength()
        {
            var ff = new FlagField();
            ff.SetBitString(1, 0, 0b11011);
            Assert.Equal(0U, ff.Bits);
        }

        [Fact]
        public void SetBitStringThrowsOutOfRangeException()
        {
            Assert.Throws<IndexOutOfRangeException>(() =>
            {
                var ff = new FlagField();
                ff.SetBitString(24, 24, 0);
            });
        }

        [Fact]
        public void ToString_()
        {
            var ff = new FlagField(42);
            Assert.Equal("00000000000000000000000000101010", ff.ToString());
        }

        [Fact]
        public void Equal()
        {
            var ff1 = new FlagField(0b1010);
            var ff2 = new FlagField(0b0101);
            var ff3 = new FlagField(0b1010);
            Assert.False(ff1 == ff2);
            Assert.True(ff1 == ff3);
        }


        [Fact]
        public void NotEqual()
        {
            var ff1 = new FlagField(0b1010);
            var ff2 = new FlagField(0b0101);
            var ff3 = new FlagField(0b1010);
            Assert.True(ff1 != ff2);
            Assert.False(ff1 != ff3);
        }
    }
}
