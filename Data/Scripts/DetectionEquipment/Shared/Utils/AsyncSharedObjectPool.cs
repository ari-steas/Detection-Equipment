using System;
using System.Collections.Generic;

namespace DetectionEquipment.Shared.Utils
{
    /// <summary>
    /// Thread-safe shared object pool with automatic factory, preparer, and cleaner. Object instances are generated and shared on a per-tick basis.
    /// </summary>
    public class AsyncSharedObjectPool<TObject> where TObject : class
    {
        private ObjectPool<TObject> _internalPool;
        private Dictionary<TObject, int> _activeObjects = new Dictionary<TObject, int>();
        private readonly object _activeLockObj = new object();
        private readonly object _nextLockObj = new object();

        private readonly Action<TObject> _preparer;
        private readonly bool _hasPreparer;

        private TObject _nextObject;
        private int _nextObjectUsers;

        public AsyncSharedObjectPool(Func<TObject> factory, Action<TObject> prepareObj = null,
            Action<TObject> cleanObj = null, int startSize = 10)
        {
            _preparer = prepareObj;
            _hasPreparer = _preparer != null;
            _internalPool = new ObjectPool<TObject>(factory, null, cleanObj, startSize);

            _nextObject = _internalPool.Pop();
            _nextObjectUsers = 0;
        }

        public void UpdateTick()
        {
            lock (_activeLockObj)
            {
                if (_nextObject != null && _nextObjectUsers > 0)
                {
                    _activeObjects[_nextObject] = _nextObjectUsers;
                }
            }

            lock (_nextLockObj)
            {
                _nextObject = _internalPool.Pop();
                _nextObjectUsers = 0;
            }

            if (_hasPreparer)
                _preparer.Invoke(_nextObject);
        }

        /// <summary>
        /// Produces an object shared for other requests within this update tick.
        /// <remarks>
        ///     The returned object is not necessarily thread-safe, and caution should be taken.
        /// </remarks>
        /// </summary>
        /// <returns></returns>
        public TObject Pop()
        {
            lock (_nextLockObj)
            {
                _nextObjectUsers++;
                return _nextObject;
            }
        }

        /// <summary>
        /// Returns an object to the internal pool.
        /// </summary>
        /// <param name="obj"></param>
        public void Push(TObject obj)
        {
            lock (_activeLockObj)
            {
                if (obj == _nextObject)
                {
                    _nextObjectUsers--;
                    return;
                }

                if (--_activeObjects[obj] > 0)
                    return;

                _activeObjects.Remove(obj);
            }

            _internalPool.Push(obj);
        }
    }
}
