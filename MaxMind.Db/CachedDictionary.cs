/*
Copyright 2010 Digital Ruby, LLC - http://www.digitalruby.com

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

#nullable disable

using System;
using System.Collections.Generic;

namespace MaxMind.Db
{
    /// <summary>
    /// Delegate that can be used to be notified when an item is removed from a CachedDictionary because the size was too big
    /// </summary>
    /// <typeparam name="TKey">Type of key</typeparam>
    /// <typeparam name="TValue">Type of value</typeparam>
    /// <param name="dictionary">Dictionary</param>
    /// <param name="key">Key</param>
    /// <param name="value">Value</param>
    internal delegate void CachedItemRemovedDelegate<TKey, TValue>(CachedDictionary<TKey, TValue> dictionary, TKey key, TValue value);

    /// <summary>
    /// A dictionary that caches up to N values in memory. Once the dictionary reaches N count, the last item in the internal list is removed.
    /// New items are always added to the start of the internal list.
    /// </summary>
    internal class CachedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDisposable
    {
        #region Private variables

        private Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> dictionary;
        private LinkedList<KeyValuePair<TKey, TValue>> priorityList;
        private int maxCount;

        #endregion Private variables

        #region Private methods

        private void MoveToFront(LinkedListNode<KeyValuePair<TKey, TValue>> node)
        {
            priorityList.Remove(node);
            priorityList.AddFirst(node);
        }

        private void InternalAdd(TKey key, TValue value)
        {
            if (dictionary.Count == maxCount)
            {
                dictionary.Remove(priorityList.Last.Value.Key);
                priorityList.RemoveLast();
            }
            priorityList.AddFirst(new KeyValuePair<TKey, TValue>(key, value));
            dictionary.Add(key, priorityList.First);
        }

        private bool InternalRemove(TKey key)
        {
            if (!dictionary.TryGetValue(key, out var node))
                return OnRemoveExternalKey(key);
            priorityList.Remove(node);
            dictionary.Remove(key);
            return true;

        }

        #endregion Private methods

        #region Protected methods

        /// <summary>
        /// Fires when a key is not found in the in memory dictionary. This gives derived classes an opportunity to look in external sources like
        /// files or databases for the value that key represents. If the derived class finds a value matching the key in the external source,
        /// then the derived class can set value and return true; when this happens the newly added value is added to the priority list.
        /// </summary>
        /// <param name="key">Key (can be replaced by the found key if desired)</param>
        /// <param name="value">Value that was found</param>
        /// <returns>True if found from external source, false if not</returns>
        protected virtual bool OnGetExternalKeyValue(ref TKey key, out TValue value)
        {
            value = default;
            return false;
        }

        /// <summary>
        /// Sets a new comparer. Clears the cache.
        /// </summary>
        /// <param name="comparer">New comparer</param>
        protected void SetComparer(IEqualityComparer<TKey> comparer)
        {
            dictionary = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(comparer);
            priorityList = new LinkedList<KeyValuePair<TKey, TValue>>();
        }

        /// <summary>
        /// Removes an external key. The key will have already been normalized. This implementation does nothing.
        /// </summary>
        /// <param name="key">Key to remove</param>
        /// <returns>True if the key was removed, false if not</returns>
        protected virtual bool OnRemoveExternalKey(TKey key)
        {
            return false;
        }

        #endregion Protected methods

        #region Public methods

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="maxCount">Maximum count the in memory dictionary will be allowed to grow to</param>
        /// <param name="comparer">Comparer for TKey (can be null for default)</param>
        public CachedDictionary(int maxCount, IEqualityComparer<TKey> comparer)
        {
            if (maxCount < 1)
            {
                throw new ArgumentOutOfRangeException("Maxcount is " + maxCount + ", it must be greater than 0");
            }
            if (comparer == null)
            {
                comparer = EqualityComparer<TKey>.Default;
            }
            this.maxCount = maxCount;
            SetComparer(comparer);
        }

        /// <summary>
        /// Disposes of all resources. Derived classes should call this base class method last.
        /// </summary>
        public virtual void Dispose()
        {
            dictionary = null;
            priorityList = null;
            maxCount = 0;
        }

        #endregion Public methods

        #region IDictionary<TKey,TValue> Members

        /// <summary>
        /// Adds a key / value pair to the dictionary. If the key already exists, it's value is replaced and moved to the front.
        /// </summary>
        /// <param name="key">Key to add</param>
        /// <param name="value">Value to add</param>
        public void Add(TKey key, TValue value)
        {
            InternalRemove(key);
            InternalAdd(key, value);
        }

        /// <summary>
        /// Checks to see if the given key is in the dictionary by calling TryGetValue.
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns>True if in dictionary, false if not</returns>
        public bool ContainsKey(TKey key)
        {
            return TryGetValue(key, out _);
        }

        /// <summary>
        /// Removes a key from memory. If there is an external source, the key will be removed from the external source if it is
        /// not in the dictionary.
        /// </summary>
        /// <param name="key">Key to remove</param>
        /// <returns>True if key was removed, false if not</returns>
        public bool Remove(TKey key)
        {
            return InternalRemove(key);
        }

        /// <summary>
        /// Attempts to get a value given a key. If the key is not found in memory, it is
        /// possible for derived classes to search an external source to find the value. In cases where this
        /// is done, the newly found item may replace the leased used item if the dictionary is at max count.
        /// </summary>
        /// <param name="key">Key to find</param>
        /// <param name="value">Found value (default of TValue if not found)</param>
        /// <returns>True if found, false if not</returns>
        public bool TryGetValue(TKey key, out TValue value)
        {
            return TryGetValueRef(ref key, out value);
        }

        /// <summary>
        /// Attempts to get a value given a key. If the key is not found in memory, it is
        /// possible for derived classes to search an external source to find the value. In cases where this
        /// is done, the newly found item may replace the leased used item if the dictionary is at max count.
        /// </summary>
        /// <param name="key">Key to find (receives the found key)</param>
        /// <param name="value">Found value (default of TValue if not found)</param>
        /// <returns>True if found, false if not</returns>
        public bool TryGetValueRef(ref TKey key, out TValue value)
        {
            if (dictionary.TryGetValue(key, out var node))
            {
                MoveToFront(node);
                value = node.Value.Value;
                key = node.Value.Key;
                return true;
            }

            if (OnGetExternalKeyValue(ref key, out value))
            {
                Add(key, value);
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Not supported
        /// </summary>
        /// <param name="key">N/A</param>
        /// <returns>N/A</returns>
        public TValue this[TKey key]
        {
            get => throw new NotSupportedException("Use TryGetValue instead");
            set => throw new NotSupportedException("Use Add instead");
        }


        /// <summary>
        /// Gets all the keys that are in memory
        /// </summary>
        public ICollection<TKey> Keys => dictionary.Keys;

        /// <summary>
        /// Gets all of the values that are in memory, external values are not returned
        /// </summary>
        public ICollection<TValue> Values
        {
            get
            {
                var values = new List<TValue>(dictionary.Values.Count);
                foreach (var node in dictionary.Values)
                {
                    values.Add(node.Value.Value);
                }
                return values;
            }
        }

        #endregion IDictionary<TKey,TValue> Members

        #region ICollection<KeyValuePair<TKey,TValue>> Members

        /// <summary>
        /// Adds an item with the key and value
        /// </summary>
        /// <param name="item">Item to add</param>
        /// <exception cref="ArgumentException">An item with the key already exists</exception>
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        /// <summary>
        /// Clears the dictionary of all items and priority information
        /// </summary>
        public void Clear()
        {
            dictionary.Clear();
            priorityList.Clear();
        }

        /// <summary>
        /// Checks to see if an item exists in the dictionary
        /// </summary>
        /// <param name="item">Item to check for</param>
        /// <returns>True if key of item exists in dictionary, false if not</returns>
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ContainsKey(item.Key);
        }

        /// <summary>
        /// Copies all items from the in memory dictionary to an array
        /// </summary>
        /// <param name="array">Array</param>
        /// <param name="arrayIndex">Start index to copy into array</param>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            foreach (var keyValue in dictionary)
            {
                array[arrayIndex++] = new KeyValuePair<TKey, TValue>(keyValue.Key, keyValue.Value.Value.Value);
            }
        }

        /// <summary>
        /// Number of items in the in memory dictionary
        /// </summary>
        public int Count => dictionary.Count;

        /// <summary>
        /// Always false
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Removes an item from the in memory dictionary
        /// </summary>
        /// <param name="item">Item to remove</param>
        /// <returns>True if an item was removed, false if not</returns>
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key);
        }

        #endregion ICollection<KeyValuePair<TKey,TValue>> Members

        #region IEnumerable<KeyValuePair<TKey,TValue>> Members

        /// <summary>
        /// Enumerates all key value pairs in the dictionary, external values are not enumerated
        /// </summary>
        /// <returns>Enumerator</returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (var node in dictionary.Values)
            {
                yield return node.Value;
            }
        }

        #endregion IEnumerable<KeyValuePair<TKey,TValue>> Members

        #region IEnumerable Members

        /// <summary>
        /// Enumerates all key value pairs in the dictionary, external values are not enumerated
        /// </summary>
        /// <returns>Enumerator</returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion IEnumerable Members
    }
}
