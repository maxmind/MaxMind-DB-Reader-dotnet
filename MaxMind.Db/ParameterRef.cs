using System;
using System.Reflection;
using System.Runtime.CompilerServices;
#if !NETSTANDARD2_0
using System.Runtime.InteropServices;
#endif

namespace MaxMind.Db
{
    /// <summary>
    /// High-performance parameter reference with embedded key optimization.
    /// Inspired by System.Text.Json's PropertyRef pattern for ultra-fast parameter lookup.
    /// </summary>
    internal readonly struct ParameterRef : IEquatable<ParameterRef>
    {
        private readonly ulong _key;
        private readonly ParameterInfo _parameterInfo;
        private readonly byte[]? _utf8Name;

        public ParameterRef(ParameterInfo parameterInfo, string parameterName)
        {
            _parameterInfo = parameterInfo;
            
            // Convert parameter name to UTF-8 bytes for consistent comparison
            var utf8Bytes = System.Text.Encoding.UTF8.GetBytes(parameterName);
            _utf8Name = utf8Bytes.Length > 7 ? utf8Bytes : null;
            _key = GetKey(utf8Bytes);
        }

        public ParameterInfo ParameterInfo => _parameterInfo;
        public ulong Key => _key;

        /// <summary>
        /// Creates an embedded key from parameter name bytes.
        /// For names ≤ 7 bytes, the entire name + length is embedded in the ulong.
        /// For longer names, creates a hash-like key + stores the full bytes separately.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong GetKey(
#if !NETSTANDARD2_0
            ReadOnlySpan<byte> name
#else
            byte[] name
#endif
            )
        {
            if (name.Length == 0) return 0;

            // Embed length in the highest byte
            ulong key = (ulong)name.Length << 56;

            if (name.Length <= 7)
            {
                // For short names, embed the entire name in the remaining 7 bytes
                for (int i = 0; i < name.Length; i++)
                {
                    key |= (ulong)name[i] << (i * 8);
                }
            }
            else
            {
                // For long names, create a hash-like key using first and last bytes
                // This provides good distribution while fitting in 7 bytes
                key |= (ulong)name[0];           // First byte
                key |= (ulong)name[1] << 8;     // Second byte  
                key |= (ulong)name[name.Length - 2] << 16; // Second to last
                key |= (ulong)name[name.Length - 1] << 24; // Last byte
                
                // Include some middle bytes for better distribution
                if (name.Length > 4)
                {
                    int mid = name.Length / 2;
                    key |= (ulong)name[mid] << 32;
                    if (name.Length > 6)
                        key |= (ulong)name[mid + 1] << 40;
                }
            }

            return key;
        }

        /// <summary>
        /// Fast parameter name comparison using embedded key optimization.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(
#if !NETSTANDARD2_0
            ReadOnlySpan<byte> parameterName,
#else
            byte[] parameterName,
#endif
            ulong key)
        {
            // Fast path: if keys don't match, names definitely don't match
            if (key != _key) return false;

            // For short names (≤7 bytes), key equality guarantees name equality
            if (parameterName.Length <= 7) return true;

            // For long names, we need to compare the full byte sequence
#if !NETSTANDARD2_0
            return _utf8Name != null && parameterName.SequenceEqual(_utf8Name);
#else
            return _utf8Name != null && ByteArrayEqual(parameterName, _utf8Name);
#endif
        }

        /// <summary>
        /// Compares with a MaxMind.Db.Key for compatibility with existing code.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Key key)
        {
            // Use the new GetUtf8Bytes method for efficient comparison
            var keyBytes = key.GetUtf8Bytes();
            ulong embeddedKey = GetKey(keyBytes);
            
            return Equals(keyBytes, embeddedKey);
        }

        public bool Equals(ParameterRef other)
        {
            return _key == other._key && 
                   ReferenceEquals(_parameterInfo, other._parameterInfo);
        }

        public override bool Equals(object? obj)
        {
            return obj is ParameterRef other && Equals(other);
        }

        public override int GetHashCode()
        {
#if !NETSTANDARD2_0
            return HashCode.Combine(_key, _parameterInfo);
#else
            return (int)(_key ^ ((ulong)_parameterInfo.GetHashCode() << 32));
#endif
        }

#if NETSTANDARD2_0
        /// <summary>
        /// Efficient byte array comparison for .NET Standard 2.0 compatibility.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ByteArrayEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
#endif
    }
}