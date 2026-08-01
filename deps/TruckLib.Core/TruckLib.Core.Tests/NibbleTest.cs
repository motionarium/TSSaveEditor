using TruckLib;

namespace TruckLib.Core.Tests
{
    public class NibbleTest
    {
        [Fact]
        public void ConstructorAndEq()
        {
            var n = new Nibble(14);
            Assert.True(n == 14);
            Assert.True(14 == n);
        }

        [Fact]
        public void Add()
        {
            Assert.True((Nibble)14 + (Nibble)3 == (Nibble)1);
        }

        [Fact]
        public void AddInt()
        {
            Assert.True((Nibble)14 + 3 == (Nibble)1);
            Assert.True(14 + (Nibble)3 == (Nibble)1);
        }

        [Fact]
        public void Increment()
        {
            Nibble n = (Nibble)14;
            n++;
            Assert.True(n == (Nibble)15);
        }

        [Fact]
        public void Subtract()
        {
            Assert.True((Nibble)1 - (Nibble)3 == (Nibble)14);
        }

        [Fact]
        public void SubtractInt()
        {
            Assert.True((Nibble)1 - 3 == (Nibble)14);
            Assert.True(1 - (Nibble)3 == (Nibble)14);
        }

        [Fact]
        public void Decrement()
        {
            Nibble n = (Nibble)14;
            n--;
            Assert.True(n == (Nibble)13);
        }

        [Fact]
        public void GreaterThan()
        {
            Assert.True((Nibble)10 > (Nibble)5);
            Assert.True(10 > (Nibble)5);
            Assert.True((Nibble)10 > 5);
            Assert.False((Nibble)5 > (Nibble)10);
            Assert.False(5 > (Nibble)10);
            Assert.False((Nibble)5 > 10);
        }

        [Fact]
        public void LessThan()
        {
            Assert.False((Nibble)10 < (Nibble)5);
            Assert.False(10 < (Nibble)5);
            Assert.False((Nibble)10 < 5);
            Assert.True((Nibble)5 < (Nibble)10);
            Assert.True(5 < (Nibble)10);
            Assert.True((Nibble)5 < 10);
        }

        [Fact]
        public void NotEquals()
        {
            Assert.True((Nibble)6 != (Nibble)9);
            Assert.True(6 != (Nibble)9);
            Assert.True((Nibble)6 != 9);
            Assert.False((Nibble)6 != (Nibble)6);
            Assert.False(6 != (Nibble)6);
            Assert.False((Nibble)6 != 6);
        }

        [Fact]
        public void ToString_()
        {
            Assert.Equal("14", ((Nibble)14).ToString());
        }

        [Fact]
        public void ThrowsIfOutOfRange()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                var n = (Nibble)(-3);
            });
            Assert.Throws<ArgumentException>(() =>
            {
                var n = (Nibble)42;
            });
        }
    }
}