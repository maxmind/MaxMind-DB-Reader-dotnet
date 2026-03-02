namespace MaxMind.Db;

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics;

/// <summary>
/// Simple non-evicting cache
/// </summary>
public class SNEGenericCache
{
    private readonly ConcurrentDictionary<(long, int, Type), (object, long)> _Cache;

    // Long for interlocked operations. Explicitly maintain our own size to avoid
    // paying for locking all buckets when alternatively checking this.Cache.Size().
    private ulong _Size;

    private readonly int _Capacity;
    private const int DEFAULT_CAPACITY = 4_096;

    /// <summary>
    /// Simple non-evicting cache
    /// </summary>
    /// <param name="maxCapacity"></param>
    public SNEGenericCache(int maxCapacity = DEFAULT_CAPACITY)
    {
        this._Cache = new();

        this._Capacity = maxCapacity;
        this._Size = 0;
    }

    /// <summary>
    /// Attempt to add an item to the cache.
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="size"></param>
    /// <param name="type"></param>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool TryAdd(long offset, int size, Type type, ValueTuple<object, long> item)
    {
        // Try a half fence first, to check if we have hit the cache limits.
        if (Volatile.Read(ref this._Size) > (ulong)this._Capacity)
        {
            return false;
        }

        // Half fence came back fine, take the full fence to increment the size.
        ulong incrementedValue = Interlocked.Increment(ref this._Size);
        
        --incrementedValue;

        // Half fence is an optimization, it may read a stale value. Here we know
        // that the capacity has been exceeded. Hopefully the next half fence
        // read will have propogated the value sufficiently.
        if (incrementedValue >= (ulong)this._Capacity)
        {
            return false;
        }

        // Else we can add. Below will most likely end up as a tail call. Do not
        // mark the method as aggressive inline.
        return this._Cache.TryAdd((offset, size, type), item);
    }

    /// <summary>
    /// Get an object from the cache
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="size"></param>
    /// <param name="type"></param>
    /// <param name="returnValue"></param>
    /// <returns></returns>
    public bool TryGet(long offset, int size, Type type, out ValueTuple<object, long> returnValue)
    {
        // Read, attempt to return a cached value
        if (this._Cache.TryGetValue((offset, size, type), out returnValue))
        {
            return true;
        }

        // We explicitly use TryGetValue first, in place of GetOrAdd. This is to
        // avoid locking on the happy path.
        //
        // If we have fallen into this logic, we had a cache miss. User must
        // attempt a cache add.

        return false;
    }

}