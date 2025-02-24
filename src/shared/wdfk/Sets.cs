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
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace WDFK;

public class SetIterator<T>(List<T> data) : IEnumerator<T>
{
    private readonly List<T> _data = data;
    public int _current = -1;

    public SetIterator(IEnumerable<T> data) : this([.. data])
    {
        Data = data;
    }

    public T Current => _data[_current];

    public IEnumerable<T> Data { get; }

    object IEnumerator.Current => Current;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public bool MoveNext()
    {
        _current++;
        return _current < _data.Count;
    }

    public void Reset()
    {
        _current = -1;
    }
}

public class Set<T> : ArrayAllocator<T>, IEnumerable<T>
{
    public override bool Equals(object obj)
    {
        if (obj is Set<T> other)
        {
            return Equal(other);
        }
        return false;
    }

    public override int GetHashCode()
    {
        int hash = 17;
        foreach (T item in _data)
        {
            hash = hash * 31 + (item == null ? 0 : item.GetHashCode());
        }
        return hash;
    }

    private readonly IComparable<T> _comparer;
    private readonly List<T> _data;

    public Comparable<T> Comparable { get; }
    public int Capacity { get; }

    public Set(IComparable<T> comparer) : this(new Comparable<T>())
    {
        _comparer = comparer;
        _data = [];
    }

    public Set(IComparable<T> comparer, int capacity) : base(capacity)
    {
        _comparer = comparer;
        _data = new List<T>(capacity);
    }
    public Set(Comparable<T> comparable)
    {
        Comparable = comparable;
        _data = [];
    }

    public Set(Comparable<T> comparable, int capacity) : this(comparable)
    {
        Capacity = capacity;
    }

    public new Set<T> Insert(T key)
    {
        if (!Find(key))
        {
            Insert(key);
        }
        return this;
    }

    public Set<T> Remove(T key)
    {
        for (int i = 0; i < Size(); i++)
        {
            if (_comparer.Equals(_data.ElementAt(i), key))
            {
                Remove((uint)i);
                break;
            }
        }
        return this;
    }

    public Set<T> Intersect(Set<T> other)
    {
        List<T> toRemove = [];
        foreach (T item in _data)
        {
            if (!other.Find(item))
            {
                toRemove.Add(item);
            }
        }
        foreach (T item in toRemove)
        {
            Remove(item);
        }
        return this;
    }

    public Set<T> Unify(Set<T> other)
    {
        foreach (T item in other)
        {
            Insert(item);
        }
        return this;
    }

    public Set<T> Exclude(Set<T> other)
    {
        foreach (T item in other)
        {
            Remove(item);
        }
        return this;
    }

    public bool Find(T key)
    {
        foreach (T item in _data)
        {
            if (_comparer.Equals(item, key))
            {
                return true;
            }
        }
        return false;
    }

    public bool Equal(Set<T> other)
    {
        foreach (T item in other)
        {
            if (!Find(item))
            {
                return false;
            }
        }
        return true;
    }

    public bool Different(Set<T> other)
    {
        return !Equal(other);
    }

    public IEnumerator<T> GetEnumerator()
    {
        return new SetIterator<T>((List<T>)_data);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public static Set<T> operator &(Set<T> left, Set<T> right)
    {
        Set<T> result = new(left._comparer);
        result.Unify(left);
        result.Intersect(right);
        return result;
    }

    public static Set<T> operator |(Set<T> left, Set<T> right)
    {
        Set<T> result = new(left._comparer);
        result.Unify(left);
        result.Unify(right);
        return result;
    }

    public static Set<T> operator ^(Set<T> left, Set<T> right)
    {
        Set<T> result = new(left._comparer);
        result.Unify(left);
        result.Exclude(right);
        return result;
    }

    public static bool operator ==(Set<T> left, Set<T> right)
    {
        return left.Equal(right);
    }

    public static bool operator !=(Set<T> left, Set<T> right)
    {
        return left.Different(right);
    }

    public static bool operator ==(Set<T> left, T right)
    {
        return left.Find(right);
    }

    public static bool operator !=(Set<T> left, T right)
    {
        return !left.Find(right);
    }

    public static Set<T> operator +(Set<T> left, T right)
    {
        Set<T> result = new(left._comparer);
        result.Unify(left);
        result.Insert(right);
        return result;
    }

    public static Set<T> operator -(Set<T> left, T right)
    {
        Set<T> result = new(left._comparer);
        result.Unify(left);
        result.Remove(right);
        return result;
    }
}

public class Vector<T> : ArrayAllocator<T>, IEnumerable<T>
{
    public override bool Equals(object obj)
    {
        if (obj is Vector<T> other)
        {
            return Equal(other);
        }
        return false;
    }

    public override int GetHashCode()
    {
        int hash = 17;
        foreach (T item in _data)
        {
            hash = hash * 31 + (item == null ? 0 : item.GetHashCode());
        }
        return hash;
    }

    private readonly IComparable<T> _comparer;
    private IEnumerable<T> _data;

    public Comparable<T> Comparable { get; }
    public int Capacity { get; }

    public Vector() : this(new Comparable<T>()) { }

    public Vector(IComparable<T> comparer)
    {
        _comparer = comparer;
    }

    public Vector(int capacity) : this(new Comparable<T>(), capacity) { }

    public Vector(IComparable<T> comparer, int capacity) : base(capacity)
    {
        _comparer = comparer;
    }

    public Vector(Comparable<T> comparable)
    {
        Comparable = comparable;
    }

    public Vector(Comparable<T> comparable, int capacity) : this(comparable)
    {
        Capacity = capacity;
    }

    public new Vector<T> Insert(T key)
    {
        Insert(key);
        return this;
    }

    public Vector<T> Insert(int index, T key)
    {
        Insert(index, key);
        return this;
    }

    public Vector<T> Remove(int index)
    {
        if (index >= Size()) return this;
        Remove(index);
        return this;
    }

    public SetIterator<T> Find(T key)
    {
        for (int i = 0; i < Size(); i++)
        {
            if (_comparer.Equals(_data.ElementAt(i), key))
            {
                return new SetIterator<T>(_data) { _current = i };
            }
        }
        return new SetIterator<T>(_data) { _current = (int)Size() };
    }

    public bool Equal(Vector<T> other)
    {
        foreach (T item in other)
        {
            if (Find(item)._current == Size())
            {
                return false;
            }
        }
        return true;
    }

    public bool Different(Vector<T> other)
    {
        return !Equal(other);
    }

    public IEnumerator<T> GetEnumerator()
    {
        return new SetIterator<T>(_data);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public T this[int index]
    {
        get
        {
            if (index < 0 || index >= Size()) throw new IndexOutOfRangeException();
            return _data.ElementAt(index);
        }
        set
        {
            if (index < 0 || index >= Size()) throw new IndexOutOfRangeException();
            var dataList = _data.ToList();
            dataList[index] = value;
            _data = dataList;
        }
    }

    public static bool operator ==(Vector<T> left, Vector<T> right)
    {
        return left.Equal(right);
    }

    public static bool operator !=(Vector<T> left, Vector<T> right)
    {
        return left.Different(right);
    }

    public static bool operator ==(Vector<T> left, T right)
    {
        return left.Find(right)._current != left.Size();
    }

    public static bool operator !=(Vector<T> left, T right)
    {
        return left.Find(right)._current == left.Size();
    }

    public static Vector<T> operator +(Vector<T> left, T right)
    {
        Vector<T> result = new Vector<T>(left._comparer);
        _ = result.Union(left);
        result.Insert(right);
        return result;
    }

    public static Vector<T> operator -(Vector<T> left, T right)
    {
        Vector<T> result = new(left._comparer);
        _ = result.Union(left);
        result.Remove(left.Find(right)._current);
        return result;
    }
}
