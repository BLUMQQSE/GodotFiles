using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class MultiMap<T, V>
{
    Dictionary<T, List<V>> _dictionary =
        new Dictionary<T, List<V>>();

    public void Add(T key, V value)
    {
        // Add a key.
        List<V> list;
        if (this._dictionary.TryGetValue(key, out list))
        {
            list.Add(value);
        }
        else
        {
            list = new List<V>();
            list.Add(value);
            this._dictionary[key] = list;
        }
    }

    public void Remove(T key, V value)
    {
        List<V> list;
        if (this._dictionary.TryGetValue(key, out list))
        {
            list.Remove(value);
        }
        if(list.Count == 0)
            _dictionary.Remove(key);
    }

    public bool Contains(T key, V value)
    {
        List<V> list;
        if (this._dictionary.TryGetValue(key, out list))
        {
            if(list.Contains(value))
                return true;
        }
        return false;
    }

    public int Count { get { return _dictionary.Count; } }

    public IEnumerable<T> Keys
    {
        get
        {
            // Get all keys.
            return this._dictionary.Keys;
        }
    }

    public List<V> this[T key]
    {
        get
        {
            // Get list at a key.
            List<V> list;
            if (!this._dictionary.TryGetValue(key, out list))
            {
                list = new List<V>();
                this._dictionary[key] = list;
            }
            return list;
        }
    }
}

