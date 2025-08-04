using System;
using System.Collections.Concurrent;
using System.Threading;

namespace MaxMind.Db
{
    /// <summary>
    /// Enhanced caching for type activators with time-based eviction
    /// </summary>
    internal sealed class TypeActivatorCache
    {
        private readonly ConcurrentDictionary<Type, CacheEntry> _cache = new();
        private long _lastEvictedTicks = DateTime.UtcNow.Ticks;
        private int _evictLock;
        
        // Cache eviction configuration
        private const long SlidingExpirationTicks = TimeSpan.TicksPerMinute; // 1 minute
        private const long EvictionIntervalTicks = TimeSpan.TicksPerSecond * 10; // 10 seconds

        public TypeActivator GetOrAdd(Type type, Func<Type, TypeActivator> factory)
        {
            var entry = _cache.GetOrAdd(type, key => new CacheEntry(factory(key)));
            
            long utcNowTicks = DateTime.UtcNow.Ticks;
            Volatile.Write(ref entry.LastUsedTicks, utcNowTicks);

            // Periodic eviction of stale entries
            if (utcNowTicks - Volatile.Read(ref _lastEvictedTicks) >= EvictionIntervalTicks)
            {
                if (Interlocked.CompareExchange(ref _evictLock, 1, 0) == 0)
                {
                    try
                    {
                        if (utcNowTicks - _lastEvictedTicks >= EvictionIntervalTicks)
                        {
                            EvictStaleEntries(utcNowTicks);
                            Volatile.Write(ref _lastEvictedTicks, utcNowTicks);
                        }
                    }
                    finally
                    {
                        Volatile.Write(ref _evictLock, 0);
                    }
                }
            }

            return entry.Activator;
        }

        public void Clear()
        {
            _cache.Clear();
            _lastEvictedTicks = DateTime.UtcNow.Ticks;
        }

        private void EvictStaleEntries(long utcNowTicks)
        {
            foreach (var kvp in _cache)
            {
                if (utcNowTicks - Volatile.Read(ref kvp.Value.LastUsedTicks) >= SlidingExpirationTicks)
                {
                    _cache.TryRemove(kvp.Key, out _);
                }
            }
        }

        private sealed class CacheEntry
        {
            public readonly TypeActivator Activator;
            public long LastUsedTicks;

            public CacheEntry(TypeActivator activator)
            {
                Activator = activator;
                LastUsedTicks = DateTime.UtcNow.Ticks;
            }
        }
    }
}