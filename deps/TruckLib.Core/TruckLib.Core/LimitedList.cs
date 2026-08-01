using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TruckLib
{
    /// <summary>
    /// Represents a list with a fixed maximum capacity.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    public class LimitedList<T> : IList<T>
    {
        /// <summary>
        /// The maximum number of elements the list can contain.
        /// </summary>
        public uint MaxCapacity { get; init; }

        private readonly List<T> list;

        /// <param name="maxCapacity">The maximum number of elements the list can contain.</param>
        public LimitedList(uint maxCapacity)
        {
            ArgumentOutOfRangeException.ThrowIfZero(maxCapacity);

            list = [];
            MaxCapacity = maxCapacity;
        }

        /// <param name="maxCapacity">The maximum number of elements the list can contain.</param>
        /// <param name="initialCapacity">The number of elements that the new list can initially store.</param>
        public LimitedList(uint maxCapacity, int initialCapacity)
        {
            ArgumentOutOfRangeException.ThrowIfZero(maxCapacity);

            if (initialCapacity > maxCapacity)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));

            list = new(initialCapacity);
            MaxCapacity = maxCapacity;
        }

        /// <param name="maxCapacity">The maximum number of elements the list can contain.</param>
        /// <param name="initialCapacity">The collection whose elements are copied to the new list.</param>
        public LimitedList(uint maxCapacity, IEnumerable<T> collection)
        {
            ArgumentOutOfRangeException.ThrowIfZero(maxCapacity);

            if (collection.Count() > maxCapacity)
                throw new ArgumentOutOfRangeException("collection.Count");

            list = new(collection);
            MaxCapacity = maxCapacity;
        }

        public T this[int index] 
        { 
            get => list[index]; 
            set => list[index] = value;
        }

        public int Count => list.Count;

        public bool IsReadOnly => false;

        public void Add(T item)
        {
            if (list.Count >= MaxCapacity)
                throw new IndexOutOfRangeException("List is full.");

            list.Add(item);
        }

        public void Clear()
        {
            list.Clear();
        }

        public bool Contains(T item)
        {
            return list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return list.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            if (list.Count >= MaxCapacity)
                throw new IndexOutOfRangeException("List is full.");
            
            if (index >= MaxCapacity)
                throw new IndexOutOfRangeException();

            list.Insert(index, item);
        }

        public bool Remove(T item)
        {
            return list.Remove(item);
        }

        public void RemoveAt(int index)
        {
            if (index >= MaxCapacity)
                throw new IndexOutOfRangeException();

            list.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
