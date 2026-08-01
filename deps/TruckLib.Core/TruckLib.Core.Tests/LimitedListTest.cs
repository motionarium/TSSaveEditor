using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib.Core.Tests
{
    public class LimitedListTest
    {
        [Fact]
        public void Constructor()
        {
            var list = new LimitedList<int>(42, 16);
            Assert.Equal(42u, list.MaxCapacity);
        }

        [Fact]
        public void ConstructorThrowsIfCapacityZero()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LimitedList<int>(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LimitedList<int>(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LimitedList<int>(0, [0,1,2]));
        }

        [Fact]
        public void ConstructorThrowsIfInitialCapacityTooLarge()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LimitedList<int>(5, 6));
        }

        [Fact]
        public void ConstructorWithList()
        {
            var orig = new List<int> { 0, 1, 2 };
            var list = new LimitedList<int>(4, orig);
            Assert.Equal(orig, list);
            Assert.Equal(4u, list.MaxCapacity);
            list[0] = 42;
            Assert.NotEqual(42, orig[0]);
        }

        [Fact]
        public void ConstructorWithListThrowsIfInitialCapacityTooSmall()
        {
            var orig = new List<int> { 0, 1, 2 };
            Assert.Throws<ArgumentOutOfRangeException>(() => new LimitedList<int>(2, orig));
        }

        [Fact]
        public void Add()
        {
            var list = new LimitedList<int>(4);
            list.Add(0);
            list.Add(1);
            list.Add(2);
            Assert.Equal([0,1,2], list);
        }

        [Fact]
        public void AddThrowsIfFull()
        {
            var list = new LimitedList<int>(4);
            list.Add(0);
            list.Add(1);
            list.Add(2);
            list.Add(3);
            Assert.Throws<IndexOutOfRangeException>(() => list.Add(4));
        }

        [Fact]
        public void Indexer()
        {
            var list = new LimitedList<int>(4) { 0, 1, 2 };
            Assert.Equal(1, list[1]);
            list[1] = 727;
            Assert.Equal(727, list[1]);
        }

        [Fact]
        public void Contains()
        {
            var list = new LimitedList<int>(4) { 0, 1, 2 };
            Assert.Contains(1, list);
            Assert.DoesNotContain(4, list);
        }

        [Fact]
        public void IndexOf()
        {
            var list = new LimitedList<int>(4) { 0, 1, 2 };
            Assert.Equal(1, list.IndexOf(1));
            Assert.Equal(-1, list.IndexOf(4));
        }

        [Fact]
        public void Insert()
        {
            var list = new LimitedList<int>(4) { 0, 1, 2 };
            list.Insert(1, 3);
            Assert.Equal([0, 3, 1, 2], list);
        }

        [Fact]
        public void InsertThrowsIfFull()
        {
            var list = new LimitedList<int>(4) { 0, 1, 2, 3 };
            Assert.Throws<IndexOutOfRangeException>(() => list.Insert(1, 4));
        }

        [Fact]
        public void InsertThrowsIfOutOfRange()
        {
            var list = new LimitedList<int>(4) { 0, 1, 2 };
            Assert.Throws<IndexOutOfRangeException>(() => list.Insert(4, 4));
        }

        [Fact]
        public void Remove()
        {
            var list = new LimitedList<int>(4) { 0, 1, 2, 3 };
            list.Remove(2);
            Assert.Equal([0, 1, 3], list);
        }

        [Fact]
        public void RemoveAt()
        {
            var list = new LimitedList<int>(4) { 0, 1, 2, 3 };
            list.RemoveAt(2);
            Assert.Equal([0, 1, 3], list);
        }
    }
}
