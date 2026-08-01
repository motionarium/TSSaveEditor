using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using TruckLib;

namespace TruckLib.Core.Tests
{
    public class MiscExtensionsTest
    {
        [Fact]
        public void Push()
        {
            int[] actual = [1, 2, 3];
            actual = actual.Push(4);
            int[] expected = [1, 2, 3, 4];
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void PushEmpty()
        {
            int[] actual = [];
            actual = actual.Push(42);
            int[] expected = [42];
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void PushNull()
        {
            int[]? actual = null;
            actual = actual.Push(42);
            int[] expected = [42];
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void QuaternionToEuler()
        {
            var q = Quaternion.CreateFromYawPitchRoll(3, 1, 2);
            var expected = new Vector3(1, 3, 2);
            var actual = q.ToEuler();
            Assert.Equal(expected.X, actual.X, 4u);
            Assert.Equal(expected.Y, actual.Y, 4u);
            Assert.Equal(expected.Z, actual.Z, 4u);
        }

        [Fact]
        public void BoolToByte()
        {
            // Quite possibly the most important unit test imaginable
            Assert.Equal((byte)0, false.ToByte());
            Assert.Equal((byte)1, true.ToByte());
        }
    }
}
