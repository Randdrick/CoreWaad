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

namespace WDFK;

public class ListEntity<T>
{
    public T Value;
    public ListEntity<T> Previous;
    public ListEntity<T>[] Next;

    public ListEntity()
    {
        Previous = null;
        Next = new ListEntity<T>[1];
    }

    public ListEntity(T value) : this()
    {
        Value = value;
    }

    public void Insert(ListEntity<T> next)
    {
        Next[0] = next;
    }

    public void Clear()
    {
        Next = new ListEntity<T>[1];
    }
}

public class ListIterator<T>
{
    private ListEntity<T> _current;

    public ListIterator()
    {
        _current = null;
    }

    public ListIterator(ListEntity<T> current)
    {
        _current = current;
    }

    public ListIterator<T> Next()
    {
        _current = _current?.Next[0];
        return this;
    }

    public ListIterator<T> Previous()
    {
        _current = _current?.Previous;
        return this;
    }

    public T Value => _current.Value;

    public static bool operator ==(ListIterator<T> a, ListIterator<T> b)
    {
        if (ReferenceEquals(a, b))
            return true;

        if (a is null || b is null)
            return false;

        return a._current == b._current;
    }

    public static bool operator !=(ListIterator<T> a, ListIterator<T> b)
    {
        return !(a == b);
    }

    public override bool Equals(object obj)
    {
        if (obj is ListIterator<T> other)
        {
            return this == other;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return _current?.GetHashCode() ?? 0;
    }

    public static implicit operator T(ListIterator<T> iterator)
    {
        return iterator._current.Value;
    }
}

public class SkipList<T>(int depth = SkipList<T>.DefaultDepth)
{
    private const int DefaultDepth = 4;
    private readonly int _depth = depth;
    private int _size;
    private ListEntity<T> _first;
    private ListEntity<T> _last;
    private readonly Comparable<T> _comparer = new();

    public void InsertAndRebuild(T key)
    {
        Insert(key);
        Rebuild();
    }

    public void RemoveAndRebuild(T key)
    {
        if (_size == 0) return;
        Remove(key);
        Rebuild();
    }

    public void Clear()
    {
        if (_size == 0) return;

        ListEntity<T> current = _first;
        while (current != null)
        {
            ListEntity<T> next = current.Next[0];
            current = next;
        }

        _first = _last = null;
        _size = 0;
    }

    public ListIterator<T> Find(T key)
    {
        ListEntity<T> current = _first;
        while (current != null)
        {
            if (_comparer.Eq(key, current.Value))
            {
                return new ListIterator<T>(current);
            }

            for (int i = current.Next.Length - 1; i >= 0; --i)
            {
                if (current.Next[i] != null && _comparer.GtOrEq(key, current.Next[i].Value))
                {
                    current = current.Next[i];
                    break;
                }
            }
        }
        return new ListIterator<T>();
    }

    public bool IsEmpty() => _size == 0;
    public int Size() => _size;

    public ListIterator<T> Begin() => new(_first);
    public ListIterator<T> End() => new();

    private void Insert(T key)
    {
        ListEntity<T> entity = new(key);

        if (_size == 0)
        {
            _first = _last = entity;
            entity.Insert(null);
        }
        else
        {
            ListEntity<T> prev = null;
            ListEntity<T> current = _first;
            while (current != null)
            {
                if (_comparer.LtOrEq(key, current.Value))
                    break;

                bool jump = false;
                for (int i = current.Next.Length - 1; i >= 0; --i)
                {
                    if (current.Next[i] != null && _comparer.GtOrEq(key, current.Next[i].Value))
                    {
                        current = current.Next[i];
                        jump = true;
                        break;
                    }
                }
                if (!jump)
                    current = current.Next[0];
            }

            prev = current?.Previous ?? _last;
            entity.Insert(current);
            entity.Previous = prev;
            if (current != null)
                current.Previous = entity;
            else
                _last = entity;
            if (prev != null)
                prev.Next[0] = entity;
            else
                _first = entity;
        }
        _size++;
    }

    private void Remove(T key)
    {
        ListEntity<T> current = _first;
        while (current != null)
        {
            if (_comparer.LtOrEq(key, current.Value))
                break;

            bool jump = false;
            for (int i = current.Next.Length - 1; i >= 0; --i)
            {
                if (current.Next[i] != null && _comparer.GtOrEq(key, current.Next[i].Value))
                {
                    current = current.Next[i];
                    jump = true;
                    break;
                }
            }
            if (!jump) current = current.Next[0];
        }

        if (current == null || !_comparer.Eq(key, current.Value)) return;

        ListEntity<T> prev = current.Previous;
        ListEntity<T> next = current.Next[0];

        if (prev != null)
            prev.Next[0] = next;
        else
            _first = next;

        if (next != null)
            next.Previous = prev;
        else
            _last = prev;

        _size--;
    }

    private void Rebuild()
    {
        if (_size == 0) return;

        ListEntity<T> refEntity = new();
        for (int i = 0; i < _depth; ++i)
            refEntity.Next[i] = null;

        ListEntity<T> current = _last;
        for (int ci = _size - 1; current != null; --ci)
        {
            current.Clear();
            int sz = _depth - (ci % _depth);
            for (int i = 0; i < sz; ++i)
            {
                current.Insert(refEntity.Next[i]);
                refEntity.Next[i] = current;
            }
            current = current.Previous;
        }
    }
}
