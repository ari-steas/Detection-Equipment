using System;
using System.Collections.Generic;

namespace DetectionEquipment.Shared.Utils
{
    /// <summary>
    /// Thread-safe object pool with automatic factory, preparer, and cleaner.
    /// </summary>
    /// <typeparam name="TObject"></typeparam>
    public class ObjectPool<TObject>
    {
        private Stack<TObject> _internalPool = new Stack<TObject>();
        private readonly object _lockObj = new object();

        private readonly Func<TObject> _factory;
        private readonly Action<TObject> _preparer, _cleaner;
        private readonly bool _hasPreparer, _hasCleaner;

        public ObjectPool(Func<TObject> factory, Action<TObject> prepareObj = null,
            Action<TObject> cleanObj = null, int startSize = 10)
        {
            _factory = factory;
            _preparer = prepareObj;
            _hasPreparer = _preparer != null;
            _cleaner = cleanObj;
            _hasCleaner = _cleaner != null;
            for (int i = 0; i < startSize; i++)
                _internalPool.Push(_factory.Invoke());
        }

        /// <summary>
        /// Requests an existing object from the pool, or generates a new one if none are available.
        /// </summary>
        /// <returns></returns>
        public TObject Pop()
        {
            TObject toPop;

            lock (_lockObj)
            {
                toPop = _internalPool.Count == 0 ? _factory.Invoke() : _internalPool.Pop();
            }

            if (_hasPreparer)
                _preparer.Invoke(toPop);
            return toPop;
        }

        /// <summary>
        /// Returns an object to the pool.
        /// </summary>
        /// <param name="obj"></param>
        public void Push(TObject obj)
        {
            if (_hasCleaner)
                _cleaner.Invoke(obj);

            lock (_lockObj)
            {
                _internalPool.Push(obj);
            }
        }
    }
}
