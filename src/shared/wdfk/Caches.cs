/*
 * Wow Arbonne Ascent Development MMORPG Server
 * Copyright (C) 2007-2025 WAAD Team <https://arbonne.games-rpg.net/>
 *
 * From original Ascent MMORPG Server, 2005-2008, which doesn't exist anymore.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *
 */

using System; 
using System.Collections.Generic;

namespace WDFK;

public class Base { }
public class Defines { }
public class Sets { }
public class Lists { }
public class Dictionaries { }

public interface IHashable<T>
{
    int HashCode(T key);
}

public interface IComparable<T>
{
    bool Eq<K>(K key1, K key2);
    bool Equals(T x, T y);
    // bool Equals<T>(object value, T key);
    bool GtOrEq<K>(K key1, K key2);
    bool LessThan(T x, T y);
    bool Lt<K>(K key1, K key2);
}

public class KeyValuePair<K, V>(K key, V value)
{
    public K Key { get; set; } = key;
    public V Value { get; set; } = value;
}

public class SmartEntry<K, V>
{
    public KeyValuePair<K, V> Pair { get; set; }
    public uint Uses { get; set; }
    public SmartEntry<K, V> Previous { get; set; }
    public SmartEntry<K, V> Next { get; set; }

    public SmartEntry()
    {
        Uses = 0;
        Previous = null;
        Next = null;
    }

    public SmartEntry(KeyValuePair<K, V> pair) : this()
    {
        Pair = pair;
    }

    public SmartEntry(SmartEntry<K, V> entry)
    {
        Pair = entry.Pair;
        Uses = entry.Uses;
        Previous = entry.Previous;
        Next = entry.Next;
    }

    public SmartEntry<K, V> Reverse()
    {
        SmartEntry<K, V> root = null;
        SmartEntry<K, V> current = this;

        while (current != null)
        {
            // Utilisation d'une affectation de tuple pour échanger les valeurs
            (current.Previous, current.Next) = (current.Next, current.Previous);

            if (current.Next == null)
                root = current;
            current = current.Previous;
        }

        return root;
    }
}

public class SmartIterator<K, V>
{
    public SmartEntry<K, V> _current;
    private readonly SmartCache<K, V> _cache;

    public SmartIterator(SmartCache<K, V> cache)
    {
        _cache = cache;
        _current = null;
    }

    public SmartIterator(SmartIterator<K, V> iterator)
    {
        _current = iterator._current;
        _cache = iterator._cache;
    }

    public void Increment()
    {
        if (_current != null && _cache != null)
            _current = _cache.GetNextEntry(_current);
    }

    public void Decrement()
    {
        if (_current != null && _cache != null)
            _current = _cache.GetPreviousEntry(_current);
    }

    public bool Equals(SmartIterator<K, V> other)
    {
        return _current == other._current;
    }

    public bool NotEquals(SmartIterator<K, V> other)
    {
        return _current != other._current;
    }

    public KeyValuePair<K, V> GetValue()
    {
        return _current.Pair;
    }

    public uint Uses()
    {
        return _current.Uses;
    }
}

public class SmartCache<K, V>
{
    private const int DefaultCapacity = 100;
    private int _size;
    private int _split;
    private int _maxSplit;
    private int _maxSize;
    private uint _uses;
    private List<SmartEntry<K, V>> _slots;
    private readonly IHashable<K> _hashable;
    private readonly IComparable<K> _comparable;

    public SmartCache(IHashable<K> hashable, IComparable<K> comparable)
    {
        _size = 0;
        _split = 0;
        _maxSplit = 1;
        _maxSize = DefaultCapacity;
        _uses = 0;
        _slots = [];
        _hashable = hashable ?? throw new ArgumentNullException(nameof(hashable));
        _comparable = comparable ?? throw new ArgumentNullException(nameof(comparable));
    }

    public SmartCache(SmartCache<K, V> cache, IHashable<K> hashable, IComparable<K> comparable)
    {
        Assign(cache);
        _hashable = hashable ?? throw new ArgumentNullException(nameof(hashable));
        _comparable = comparable ?? throw new ArgumentNullException(nameof(comparable));
    }

    public void Assign(SmartCache<K, V> cache)
    {
        Clear();
        _size = cache._size;
        _split = cache._split;
        _maxSplit = cache._maxSplit;
        _slots = [.. cache._slots];
        _maxSize = cache._maxSize;
        _uses = cache._uses;

        foreach (var entry in cache._slots)
        {
            Insert(entry.Pair);
        }

        if (_size != cache._size)
            Clear();
    }

    public void Clear()
    {
        _split = _size = 0;
        _maxSplit = 1;

        foreach (var entry in _slots)
        {
            var current = entry;
            while (current != null)
            {
                var next = current.Next;
                // Assuming SmartEntry is a reference type and needs disposal
                current = next;
            }
        }

        _slots.Clear();
    }

    public void Insert(KeyValuePair<K, V> pair)
    {
        if (_size >= _maxSize)
        {
            SmartEntry<K, V> leastUsed = null;
            double leastStat = double.MaxValue;

            foreach (var slot in _slots)
            {
                var currentEntry = slot;
                while (currentEntry != null)
                {
                    double currentStat = GetStat(currentEntry);
                    if (currentStat < leastStat)
                    {
                        leastUsed = currentEntry;
                        leastStat = currentStat;
                    }
                    currentEntry = currentEntry.Next;
                }
            }

            if (leastUsed != null)
                Remove(leastUsed.Pair.Key);
        }

        _slots.Add(null);
        if (_slots.Count <= _size) return;

        int bucket = BucketOffset(_maxSplit, _split, _hashable.HashCode(pair.Key));
        var slotEntry = _slots[bucket];
        SmartEntry<K, V> newEntry;

        if (slotEntry == null || _comparable.LessThan(pair.Key, slotEntry.Pair.Key))
        {
            newEntry = new SmartEntry<K, V>(pair);
            newEntry.Next = slotEntry;
            if (slotEntry != null) slotEntry.Previous = newEntry;
            _slots[bucket] = newEntry;
            _size++;
            SplitBucket();
        }
        else if (_comparable.Equals(pair.Key, slotEntry.Pair.Key))
        {
            _slots.RemoveAt(_slots.Count - 1);
        }
        else
        {
            var prevEntry = slotEntry;
            slotEntry = slotEntry.Next;
            while (slotEntry != null)
            {
                if (_comparable.LessThan(pair.Key, slotEntry.Pair.Key))
                {
                    newEntry = new SmartEntry<K, V>(pair);
                    prevEntry.Next = newEntry;
                    newEntry.Previous = prevEntry;
                    _size++;
                    SplitBucket();
                    break;
                }
                else if (_comparable.Equals(pair.Key, slotEntry.Pair.Key))
                {
                    _slots.RemoveAt(_slots.Count - 1);
                    break;
                }
                prevEntry = slotEntry;
                slotEntry = slotEntry.Next;
            }
        }
    }

    public void Insert(K key, V value)
    {
        Insert(new KeyValuePair<K, V>(key, value));
    }

    public void Remove(K key)
    {
        if (_size == 0) return;

        int bucket = BucketOffset(_maxSplit, _split, _hashable.HashCode(key));
        var entry = _slots[bucket];

        if (entry != null)
        {
            if (_comparable.Equals(key, entry.Pair.Key))
            {
                _slots[bucket] = entry.Next;
                if (entry.Next != null) entry.Next.Previous = null;
                _size--;
                MergeBucket();
                _slots.RemoveAt(_slots.Count - 1);
                // Assuming SmartEntry is a reference type and needs disposal
            }
            else
            {
                var prev = entry;
                entry = entry.Next;
                while (entry != null)
                {
                    if (_comparable.Equals(key, entry.Pair.Key))
                    {
                        prev.Next = entry.Next;
                        if (entry.Next != null) entry.Next.Previous = prev;
                        _size--;
                        MergeBucket();
                        _slots.RemoveAt(_slots.Count - 1);
                        // Assuming SmartEntry is a reference type and needs disposal
                        break;
                    }
                    prev = entry;
                    entry = entry.Next;
                }
            }
        }
    }

    public SmartIterator<K, V> Find(K key)
    {
        var iterator = new SmartIterator<K, V>(this)
        {
            _current = GetEntry(key)
        };
        if (iterator._current != null) iterator._current.Uses++;
        _uses++;
        return iterator;
    }

    public bool Has(K key)
    {
        if (_size == 0) return false;

        int bucket = BucketOffset(_maxSplit, _split, _hashable.HashCode(key));
        var entry = _slots[bucket];

        while (entry != null)
        {
            if (_comparable.Equals(key, entry.Pair.Key))
                return true;
            entry = entry.Next;
        }

        return false;
    }

    public V this[K key]
    {
        get
        {
            var entry = GetEntry(key);
            if (entry != null)
            {
                entry.Uses++;
                _uses++;
                return entry.Pair.Value;
            }
            return default;
        }
    }

    public double GetStat(K key)
    {
        var entry = GetEntry(key);
        return GetStat(entry);
    }

    public bool IsEmpty()
    {
        return _size == 0;
    }

    public int Size()
    {
        return _size;
    }

    public uint Uses()
    {
        return _uses;
    }

    public SmartIterator<K, V> Begin()
    {
        var iterator = new SmartIterator<K, V>(this);
        if (!IsEmpty())
            iterator._current = _slots[0];
        return iterator;
    }

    public SmartIterator<K, V> End()
    {
        return new SmartIterator<K, V>(this);
    }

    private double GetStat(SmartEntry<K, V> entry)
    {
        return entry != null ? (double)entry.Uses / _uses : 0;
    }

    private SmartEntry<K, V> GetEntry(K key)
    {
        if (_size == 0) return null;

        int bucket = BucketOffset(_maxSplit, _split, _hashable.HashCode(key));
        var entry = _slots[bucket];

        while (entry != null)
        {
            if (_comparable.Equals(key, entry.Pair.Key))
                return entry;
            entry = entry.Next;
        }

        return null;
    }

    public SmartEntry<K, V> GetNextEntry(SmartEntry<K, V> entry)
    {
        if (_size == 0 || entry == null) return null;

        int bucket = BucketOffset(_maxSplit, _split, _hashable.HashCode(entry.Pair.Key));
        var current = _slots[bucket];

        while (current != null)
        {
            if (_comparable.Equals(entry.Pair.Key, current.Pair.Key))
            {
                current = current.Next;
                if (current == null)
                {
                    do
                    {
                        if (++bucket == _size) return null;
                        current = _slots[bucket];
                    } while (current == null);
                }
                return current;
            }
            current = current.Next;
        }

        return null;
    }

    public SmartEntry<K, V> GetPreviousEntry(SmartEntry<K, V> entry)
    {
        if (_size == 0 || entry == null) return null;

        int bucket = BucketOffset(_maxSplit, _split, _hashable.HashCode(entry.Pair.Key));
        var current = _slots[bucket];

        while (current != null)
        {
            if (_comparable.Equals(entry.Pair.Key, current.Pair.Key))
            {
                current = current.Previous;
                if (current == null)
                {
                    do
                    {
                        if (--bucket == _size) return null;
                        current = _slots[bucket];
                    } while (current == null);
                }
                return current;
            }
            current = current.Previous;
        }

        return null;
    }

    private static int BucketOffset(int maxSplit, int split, int hashValue)
    {
        return (hashValue % maxSplit < split) ? hashValue % (maxSplit << 1) : hashValue % maxSplit;
    }

    private void SplitBucket()
    {
        SmartEntry<K, V> oldChain = null;
        SmartEntry<K, V> newChain = null;
        SmartEntry<K, V> prev = null;

        int oldPlace = _split;
        int newPlace = oldPlace + _maxSplit;

        if (oldPlace + 1 >= _maxSplit)
        {
            if (_size < 2) return;
            _maxSplit <<= 1;
            _split = 0;
        }
        else
        {
            _split = oldPlace + 1;
        }

        SmartEntry<K, V> current = _slots[oldPlace];

        while (current != null)
        {
            SmartEntry<K, V> next = current.Next;
            current.Previous = prev;

            if (BucketOffset(_maxSplit, _split, _hashable.HashCode(current.Pair.Key)) == oldPlace)
            {
                current.Next = oldChain;
                if (oldChain != null) oldChain.Previous = current;
                oldChain = current;
            }
            else
            {
                current.Next = newChain;
                if (newChain != null) newChain.Previous = current;
                newChain = current;
            }

            prev = current;
            current = next;
        }

        if (oldChain != null)
            _slots[oldPlace] = oldChain.Reverse();
        else
            _slots[oldPlace] = null;

        if (newChain != null)
            _slots[newPlace] = newChain.Reverse();
        else
            _slots[newPlace] = null;
    }

    private void MergeBucket()
    {
        if (_split == 0)
        {
            if (_size < 1) return;
            _maxSplit >>= 1;
            _split = _maxSplit - 1;
        }
        else
        {
            _split--;
        }

        int newPlace = _split;
        int oldPlace = newPlace + _maxSplit;
        bool useOldChain;

        var oldChain = _slots[oldPlace];
        var newChain = _slots[newPlace];
        SmartEntry<K, V> mergeList = null;

        while (oldChain != null || newChain != null)
        {
            if (oldChain == null)
            {
                useOldChain = false;
            }
            else if (newChain == null)
            {
                useOldChain = true;
            }
            else
            {
                useOldChain = _comparable.LessThan(oldChain.Pair.Key, newChain.Pair.Key);
            }

            if (useOldChain)
            {
                var oldChainNext = oldChain.Next;
                oldChain.Next = mergeList;
                mergeList = oldChain;
                oldChain = oldChainNext;
            }
            else
            {
                var newChainNext = newChain.Next;
                newChain.Next = mergeList;
                mergeList = newChain;
                newChain = newChainNext;
            }
        }

        if (mergeList != null)
            _slots[newPlace] = mergeList.Reverse();
        else
            _slots[newPlace] = null;

        _slots[oldPlace] = null;
    }
}
