using System;

namespace MaxMind.Db;

/// <summary>
/// Delegate methods for allocations
/// </summary>
public static class AllocatorDelegates
{
    /// <summary>
    /// Allocate a string based on a sequence of bytes
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public delegate string GetString(ReadOnlyMemory<byte> bytes);
}