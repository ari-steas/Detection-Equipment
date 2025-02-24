using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace DetectionEquipment.Shared
{
    public class OrderedDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private List<TKey> _keys;
        private List<TValue> _values;

        public OrderedDictionary()
        {
            _keys = new List<TKey>();
            _values = new List<TValue>();
        }

        public OrderedDictionary(Dictionary<TKey, TValue> dictionary)
        {
            _keys = dictionary.Keys.ToList();
            _values = dictionary.Values.ToList();
        }

        public TValue this[TKey key]
        {
            get
            {
                return _values[_keys.IndexOf(key)];
            }
            set
            {
                int idx = _keys.IndexOf(key);
                if (idx != -1)
                    _values[idx] = value;
                else
                {
                    _keys.Add(key);
                    _values.Add(value);
                }
            }
        }

        public ICollection<TKey> Keys => _keys;

        public ICollection<TValue> Values => _values;

        public int Count => _keys.Count;

        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value)
        {
            this[key] = value;
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            this[item.Key] = item.Value;
        }

        public TKey GetKey(int index) => _keys[index];
        public TValue GetValue(int index) => _values[index];

        public void SetKey(int index, TKey key) => _keys[index] = key;
        public void SetValue(int index, TValue value) => _values[index] = value;

        public void Clear()
        {
            _keys.Clear();
            _values.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            int idx = _keys.IndexOf(item.Key);
            return idx != -1 && _values[idx].Equals(item.Value);
        }

        public bool ContainsKey(TKey key) => _keys.Contains(key);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            throw new Exception("Not implemented."); // we can't use NotImplementedExceptions
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            var dict = new Dictionary<TKey, TValue>(_keys.Count);
            for (int i = 0; i < _keys.Count; i++)
                dict.Add(_keys[i], _values[i]);
            return dict.GetEnumerator();
        }

        public bool Remove(TKey key)
        {
            int idx = _keys.IndexOf(key);
            if (idx == -1)
                return false;
            _keys.RemoveAt(idx);
            _values.RemoveAt(idx);
            return true;
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            int idx = _keys.IndexOf(item.Key);
            if (idx == -1 || !item.Value.Equals(_values[idx]))
                return false;
            _keys.RemoveAt(idx);
            _values.RemoveAt(idx);
            return true;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            int idx = _keys.IndexOf(key);
            if (idx == -1)
            {
                value = default(TValue);
                return false;
            }
            value = _values[idx];
            return true;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
