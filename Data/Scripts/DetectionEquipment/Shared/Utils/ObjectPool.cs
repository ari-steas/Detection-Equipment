using System;
using System.Collections.Generic;

namespace DetectionEquipment.Shared.Utils
{
    /// <summary>
    /// Thread-safe object pool with automatic factory and cleaner.
    /// </summary>
    /// <typeparam name="TObject"></typeparam>
    internal class ObjectPool<TObject>
    {
        private Stack<TObject> _internalPool = new Stack<TObject>();
        private readonly object _lockObj = new object();

        private readonly Func<TObject> _factory;
        private readonly Action<TObject> _cleaner;
        private readonly bool _hasCleaner;

        public ObjectPool(Func<TObject> factory, Action<TObject> cleanObj = null, int startSize = 10)
        {
            _factory = factory;
            _cleaner = cleanObj;
            _hasCleaner = _cleaner != null;
            for (int i = 0; i < startSize; i++)
                _internalPool.Push(_factory.Invoke());
        }

        /// <summary>
        /// Requests an existing object from the pool, or generates a new one if none are available.
        /// </summary>
        /// <returns></returns>
        public TObject Pull()
        {
            lock (_lockObj)
            {
                if (_internalPool.Count == 0)
                    return _factory.Invoke();
                return _internalPool.Pop();
            }
        }

        /// <summary>
        /// Returns an object to the pool.
        /// </summary>
        /// <param name="obj"></param>
        public void Push(TObject obj)
        {
            lock (_lockObj)
            {
                if (_hasCleaner)
                    _cleaner.Invoke(obj);
                _internalPool.Push(obj);
            }
        }
    }
}
