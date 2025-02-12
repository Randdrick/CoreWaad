using System;
using System.Collections.Generic;

namespace WDFK
{
    // Structure de stockage interne
    public class HashEntry<K, V>
    {
        public KeyValuePair<K, V> Pair { get; set; }
        public HashEntry<K, V> Previous { get; set; }
        public HashEntry<K, V> Next { get; set; }

        public HashEntry() { }
        public HashEntry(KeyValuePair<K, V> pair)
        {
            Pair = pair;
        }

        public HashEntry<K, V> Reverse()
        {
            HashEntry<K, V> root = null;
            HashEntry<K, V> prev = null;
            HashEntry<K, V> current = this;

            while (current != null)
            {
                HashEntry<K, V> next = current.Next;
                (current.Next, current.Previous) = (current.Previous, current.Next); // Utilisation de tuple pour échanger les valeurs

                if (next == null)
                {
                    root = current;
                }

                prev = current;
                current = next;
            }

            return root;
        }
    }

    public class Dictionary<K, V>
    {
        private List<HashEntry<K, V>>[] m_Slots;
        private int m_Size;
        private int m_Split;
        private int m_MaxSplit;
        private readonly IHashable<K> m_Hashable;
        private readonly IComparable<K> m_Comparable;

        public Dictionary()
        {
            m_Size = 0;
            m_Split = 0;
            m_MaxSplit = 1;
            m_Slots = new List<HashEntry<K, V>>[m_MaxSplit];
            // m_Hashable = new Hashable<K>();
            // m_Comparable = new Comparable<K>();
        }

        public void Clear()
        {
            m_Size = 0;
            m_Split = 0;
            m_MaxSplit = 1;
            m_Slots = new List<HashEntry<K, V>>[m_MaxSplit];
        }

        public void Insert(K key, V value)
        {
            Insert(new KeyValuePair<K, V>(key, value));
        }

        public void Insert(KeyValuePair<K, V> pair)
        {
            int bucket = BucketOffset(m_MaxSplit, m_Split, m_Hashable.HashCode(pair.Key));

            if (m_Slots[bucket] == null)
            {
                m_Slots[bucket] = [];
            }

            HashEntry<K, V> newEntry = new(pair);
            HashEntry<K, V> current = m_Slots[bucket].Count > 0 ? m_Slots[bucket][0] : null;

            if (current == null || m_Comparable.Lt(pair.Key, current.Pair.Key))
            {
                newEntry.Next = current;
                if (current != null)
                {
                    current.Previous = newEntry;
                }
                m_Slots[bucket][0] = newEntry;
                m_Size++;
                SplitBucket();
            }
            else if (m_Comparable.Eq(pair.Key, current.Pair.Key))
            {
                current.Pair.Value = pair.Value;
            }
            else
            {
                while (current != null && m_Comparable.GtOrEq(pair.Key, current.Pair.Key))
                {
                    if (current.Next == null || m_Comparable.Lt(pair.Key, current.Next.Pair.Key))
                    {
                        newEntry.Next = current.Next;
                        newEntry.Previous = current;
                        if (current.Next != null)
                        {
                            current.Next.Previous = newEntry;
                        }
                        current.Next = newEntry;
                        m_Size++;
                        SplitBucket();
                        break;
                    }
                    current = current.Next;
                }
            }
        }

        public bool Remove(K key)
        {
            int bucket = BucketOffset(m_MaxSplit, m_Split, m_Hashable.HashCode(key));
            HashEntry<K, V> current = m_Slots[bucket]?.Count > 0 ? m_Slots[bucket][0] : null;

            while (current != null)
            {
                if (m_Comparable.Eq(key, current.Pair.Key))
                {
                    if (current.Previous != null)
                    {
                        current.Previous.Next = current.Next;
                    }
                    else
                    {
                        m_Slots[bucket][0] = current.Next;
                    }

                    if (current.Next != null)
                    {
                        current.Next.Previous = current.Previous;
                    }

                    m_Size--;
                    MergeBucket();
                    return true;
                }
                current = current.Next;
            }

            return false;
        }

        public bool ContainsKey(K key)
        {
            int bucket = BucketOffset(m_MaxSplit, m_Split, m_Hashable.HashCode(key));
            HashEntry<K, V> current = m_Slots[bucket]?.Count > 0 ? m_Slots[bucket][0] : null;

            while (current != null)
            {
                if (m_Comparable.Eq(key, current.Pair.Key))
                {
                    return true;
                }
                current = current.Next;
            }

            return false;
        }

        public V this[K key]
        {
            get
            {
                int bucket = BucketOffset(m_MaxSplit, m_Split, m_Hashable.HashCode(key));
                HashEntry<K, V> current = m_Slots[bucket]?.Count > 0 ? m_Slots[bucket][0] : null;

                while (current != null)
                {
                    if (m_Comparable.Eq(key, current.Pair.Key))
                    {
                        return current.Pair.Value;
                    }
                    current = current.Next;
                }

                throw new KeyNotFoundException();
            }
        }

        public int Size => m_Size;

        private static int BucketOffset(int maxSplit, int splitPtr, int hashVal)
        {
            return (hashVal % maxSplit < splitPtr) ? hashVal % (maxSplit << 1) : hashVal % maxSplit;
        }

        private void SplitBucket()
        {
            if (m_Split + 1 >= m_MaxSplit)
            {
                m_MaxSplit <<= 1;
                Array.Resize(ref m_Slots, m_MaxSplit);
                m_Split = 0;
            }
            else
            {
                m_Split++;
            }

            int oldPlace = m_Split - 1;
            int newPlace = oldPlace + m_MaxSplit;

            List<HashEntry<K, V>> oldChain = [];
            List<HashEntry<K, V>> newChain = [];

            HashEntry<K, V> current = m_Slots[oldPlace]?.Count > 0 ? m_Slots[oldPlace][0] : null;

            while (current != null)
            {
                HashEntry<K, V> next = current.Next;
                int bucket = BucketOffset(m_MaxSplit, m_Split, m_Hashable.HashCode(current.Pair.Key));

                if (bucket == oldPlace)
                {
                    oldChain.Add(current);
                }
                else
                {
                    newChain.Add(current);
                }

                current = next;
            }

            oldChain.Reverse();
            newChain.Reverse();

            m_Slots[oldPlace] = [.. oldChain];
            m_Slots[newPlace] = [.. newChain];
        }

        private void MergeBucket()
        {
            if (m_Split == 0)
            {
                m_MaxSplit >>= 1;
                Array.Resize(ref m_Slots, m_MaxSplit);
                m_Split = m_MaxSplit - 1;
            }
            else
            {
                m_Split--;
            }

            int newPlace = m_Split;
            int oldPlace = newPlace + m_MaxSplit;

            List<HashEntry<K, V>> mergeList = [];
            HashEntry<K, V> oldChain = m_Slots[oldPlace]?.Count > 0 ? m_Slots[oldPlace][0] : null;
            HashEntry<K, V> newChain = m_Slots[newPlace]?.Count > 0 ? m_Slots[newPlace][0] : null;

            while (oldChain != null || newChain != null)
            {
                if (oldChain == null)
                {
                    mergeList.Add(newChain);
                    newChain = newChain.Next;
                }
                else if (newChain == null)
                {
                    mergeList.Add(oldChain);
                    oldChain = oldChain.Next;
                }
                else if (m_Comparable.Lt(oldChain.Pair.Key, newChain.Pair.Key))
                {
                    mergeList.Add(oldChain);
                    oldChain = oldChain.Next;
                }
                else
                {
                    mergeList.Add(newChain);
                    newChain = newChain.Next;
                }
            }

            mergeList.Reverse();
            m_Slots[newPlace] = [.. mergeList];
            m_Slots[oldPlace] = null;
        }
    }
}
