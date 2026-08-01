using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.Core.Tests
{
    public class MathExtensionsTest
    {
        [Fact]
        public void ToEuler()
        {
            var quaternion = new Quaternion(0.1f, 0.2f, 0.3f, 0.4f);
            var expected = new Vector3(-0.1337316f, 0.8329813f, 1.2277724f);
            var actual = quaternion.ToEuler();
            Assert.Equal(expected.X, actual.X, 0.0001);
            Assert.Equal(expected.Y, actual.Y, 0.0001);
            Assert.Equal(expected.Z, actual.Z, 0.0001);
        }

        [Fact]
        public void ToEulerDeg()
        {
            var quaternion = new Quaternion(0.1f, 0.2f, 0.3f, 0.4f);
            var expected = new Vector3(-7.6622561f, 47.7263108f, 70.3461755f);
            var actual = quaternion.ToEulerDeg();
            Assert.Equal(expected.X, actual.X, 0.0001);
            Assert.Equal(expected.Y, actual.Y, 0.0001);
            Assert.Equal(expected.Z, actual.Z, 0.0001);
        }
    }
}
