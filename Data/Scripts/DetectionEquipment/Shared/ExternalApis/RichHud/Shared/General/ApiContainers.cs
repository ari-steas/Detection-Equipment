using System;
using System.Collections;
using System.Collections.Generic;
using VRage;

namespace RichHudFramework
{
    /// <summary>
    ///     Read-only collection of cached and indexed RHF API wrappers
    /// </summary>
    public class ReadOnlyApiCollection<TValue> : IReadOnlyList<TValue>, IIndexedCollection<TValue>
    {
        protected readonly CollectionDataEnumerator<TValue> enumerator;
        protected readonly Func<int> GetCountFunc;

        protected readonly Func<int, TValue> GetNewWrapperFunc;
        protected readonly List<TValue> wrapperList;

        public ReadOnlyApiCollection(Func<int, TValue> GetNewWrapper, Func<int> getCount)
        {
            GetNewWrapperFunc = GetNewWrapper;
            GetCountFunc = getCount;

            wrapperList = new List<TValue>();
            enumerator = new CollectionDataEnumerator<TValue>(x => this[x], getCount);
        }

        public ReadOnlyApiCollection(MyTuple<Func<int, TValue>, Func<int>> tuple)
            : this(tuple.Item1, tuple.Item2)
        {
        }

        /// <summary>
        ///     Returns the element at the given index.
        /// </summary>
        public virtual TValue this[int index]
        {
            get
            {
                var count = GetCountFunc();

                if (index >= count)
                    throw new Exception(
                        $"Index ({index}) was out of Range. Must be non-negative and less than {count}.");

                while (wrapperList.Count < count)
                    for (var n = wrapperList.Count; wrapperList.Count < count; n++)
                        wrapperList.Add(GetNewWrapperFunc(n));

                if (count > 9 && wrapperList.Count > count * 3)
                {
                    wrapperList.RemoveRange(count, wrapperList.Count - count);
                    wrapperList.TrimExcess();
                }

                return wrapperList[index];
            }
        }

        /// <summary>
        ///     Returns the number of elements in the collection
        /// </summary>
        public virtual int Count => GetCountFunc();

        public virtual IEnumerator<TValue> GetEnumerator()
        {
            return enumerator;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>
    ///     Read-only collection backed by delegates
    /// </summary>
    public class ReadOnlyCollectionData<TValue> : IReadOnlyList<TValue>, IIndexedCollection<TValue>
    {
        protected readonly CollectionDataEnumerator<TValue> enumerator;
        protected readonly Func<int> GetCountFunc;

        protected readonly Func<int, TValue> Getter;

        public ReadOnlyCollectionData(Func<int, TValue> Getter, Func<int> getCount)
        {
            this.Getter = Getter;
            GetCountFunc = getCount;
            enumerator = new CollectionDataEnumerator<TValue>(x => this[x], getCount);
        }

        public ReadOnlyCollectionData(MyTuple<Func<int, TValue>, Func<int>> tuple)
            : this(tuple.Item1, tuple.Item2)
        {
        }

        /// <summary>
        ///     Returns the element at the given index.
        /// </summary>
        public virtual TValue this[int index] => Getter(index);

        /// <summary>
        ///     Returns the number of elements in the collection
        /// </summary>
        public virtual int Count => GetCountFunc();

        public virtual IEnumerator<TValue> GetEnumerator()
        {
            return enumerator;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}