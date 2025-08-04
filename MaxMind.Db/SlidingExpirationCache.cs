using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace MaxMind.Db
{
    /// <summary>
    /// High-performance cache with sliding expiration for automatic cleanup of unused entries.
    /// Inspired by System.Text.Json's caching strategy to prevent memory leaks in long-running applications.
    /// </summary>
    internal sealed class SlidingExpirationCache<TKey, TValue> where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, CacheEntry> _cache = new();
        private readonly long _slidingExpirationTicks;
        private readonly long _evictionIntervalTicks;
        private long _lastEvictedTicks;

        public SlidingExpirationCache(TimeSpan slidingExpiration, TimeSpan evictionInterval)
        {
            _slidingExpirationTicks = slidingExpiration.Ticks;
            _evictionIntervalTicks = evictionInterval.Ticks;
            _lastEvictedTicks = DateTime.UtcNow.Ticks;
        }

        /// <summary>
        /// Gets or adds a value to the cache with sliding expiration.
        /// </summary>
        /// <param name="key">The cache key</param>
        /// <param name="valueFactory">Factory function to create the value if not in cache</param>
        /// <returns>The cached or newly created value</returns>
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            var entry = _cache.GetOrAdd(key, k => new CacheEntry(valueFactory(k)));

            // Update last used timestamp for sliding expiration
            Volatile.Write(ref entry.LastUsedTicks, DateTime.UtcNow.Ticks);

            // Periodic cleanup of expired entries
            TryEvictExpiredEntries();

            return entry.Value;
        }

        /// <summary>
        /// Gets the number of items currently in the cache.
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// Clears all entries from the cache.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            _lastEvictedTicks = DateTime.UtcNow.Ticks;
        }

        /// <summary>
        /// Tries to evict expired entries if enough time has passed since the last eviction.
        /// This method is thread-safe and designed to be called frequently with minimal overhead.
        /// </summary>
        private void TryEvictExpiredEntries()
        {
            long currentTicks = DateTime.UtcNow.Ticks;

            // Only evict if enough time has passed since last eviction
            if (currentTicks - Volatile.Read(ref _lastEvictedTicks) < _evictionIntervalTicks)
                return;

            // Use compare-and-swap to ensure only one thread performs eviction
            if (Interlocked.CompareExchange(ref _lastEvictedTicks, currentTicks, _lastEvictedTicks) != _lastEvictedTicks)
                return;

            // Find and remove expired entries
            var expiredKeys = new List<TKey>();
            long expirationThreshold = currentTicks - _slidingExpirationTicks;

            foreach (var kvp in _cache)
            {
                // Use volatile read to get the most recent timestamp
                long lastUsed = Volatile.Read(ref kvp.Value.LastUsedTicks);
                if (lastUsed < expirationThreshold)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            // Remove expired entries
            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Cache entry with sliding expiration timestamp.
        /// </summary>
        private sealed class CacheEntry
        {
            public readonly TValue Value;
            public long LastUsedTicks;

            public CacheEntry(TValue value)
            {
                Value = value;
                LastUsedTicks = DateTime.UtcNow.Ticks;
            }
        }
    }

    /// <summary>
    /// Global cache manager for TypeActivator instances with intelligent cleanup.
    /// Provides automatic memory management for long-running applications.
    /// </summary>
    internal static class TypeActivatorCache
    {
        // Default expiration: 30 minutes of inactivity
        private static readonly TimeSpan DefaultSlidingExpiration = TimeSpan.FromMinutes(30);

        // Eviction check interval: every 5 minutes
        private static readonly TimeSpan DefaultEvictionInterval = TimeSpan.FromMinutes(5);

        private static readonly SlidingExpirationCache<Type, TypeActivator> _cache =
            new(DefaultSlidingExpiration, DefaultEvictionInterval);

        /// <summary>
        /// Gets or creates a TypeActivator for the specified type with automatic caching.
        /// </summary>
        /// <param name="type">The type to create an activator for</param>
        /// <param name="activatorFactory">Factory function to create the TypeActivator</param>
        /// <returns>A cached or newly created TypeActivator</returns>
        public static TypeActivator GetOrAdd(Type type, Func<Type, TypeActivator> activatorFactory)
        {
            return _cache.GetOrAdd(type, activatorFactory);
        }

        /// <summary>
        /// Gets the number of cached TypeActivator instances.
        /// Useful for monitoring cache effectiveness.
        /// </summary>
        public static int CacheCount => _cache.Count;

        /// <summary>
        /// Manually clears the TypeActivator cache.
        /// Useful for testing or memory pressure scenarios.
        /// </summary>
        internal static void ClearCache()
        {
            _cache.Clear();
        }

#if DEBUG
        /// <summary>
        /// Performance counters for cache effectiveness monitoring (DEBUG builds only).
        /// </summary>
        internal static class PerformanceCounters
        {
            private static long _cacheHits;
            private static long _cacheMisses;

            public static void RecordCacheHit() => Interlocked.Increment(ref _cacheHits);
            public static void RecordCacheMiss() => Interlocked.Increment(ref _cacheMisses);

            public static long CacheHits => Volatile.Read(ref _cacheHits);
            public static long CacheMisses => Volatile.Read(ref _cacheMisses);

            public static double CacheHitRatio =>
                _cacheHits + _cacheMisses == 0 ? 0.0 : (double)_cacheHits / (_cacheHits + _cacheMisses);

            internal static void Reset()
            {
                Volatile.Write(ref _cacheHits, 0);
                Volatile.Write(ref _cacheMisses, 0);
            }
        }
#endif
    }
}