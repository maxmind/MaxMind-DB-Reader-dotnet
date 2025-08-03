using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MaxMind.Db
{
    /// <summary>
    /// High-performance parameter dictionary using ParameterRef pattern for embedded key optimization.
    /// Inspired by System.Text.Json's PropertyRef caching strategy with locality-aware search.
    /// </summary>
    internal sealed class OptimizedParameterDictionary : IParameterDictionary
    {
        private const int MaxLinearSearchSize = 16;
        private ParameterRef[] _parameterRefs = Array.Empty<ParameterRef>();
        private Dictionary<Key, ParameterInfo>? _fallbackDict;
        private int _lastAccessIndex; // Locality of reference optimization

        public void Add(Key key, ParameterInfo value)
        {
            if (_fallbackDict != null)
            {
                _fallbackDict.Add(key, value);
                return;
            }

            if (_parameterRefs.Length < MaxLinearSearchSize)
            {
                // Extract parameter name from the key for ParameterRef creation
                string parameterName = ExtractParameterName(key, value);
                var parameterRef = new ParameterRef(value, parameterName);
                
                // Resize array and add new parameter reference
                var newRefs = new ParameterRef[_parameterRefs.Length + 1];
                Array.Copy(_parameterRefs, newRefs, _parameterRefs.Length);
                newRefs[_parameterRefs.Length] = parameterRef;
                _parameterRefs = newRefs;
            }
            else
            {
                // Convert to Dictionary for large parameter sets
                ConvertToFallbackDictionary();
                _fallbackDict!.Add(key, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(Key key, out ParameterInfo value)
        {
            if (_fallbackDict != null)
            {
                return _fallbackDict.TryGetValue(key, out value!);
            }

            // Fast path using ParameterRef array with locality-aware search
            // Start from last accessed index to take advantage of locality of reference
            int startIndex = _lastAccessIndex;
            
            for (int i = 0; i < _parameterRefs.Length; i++)
            {
                int index = (startIndex + i) % _parameterRefs.Length;
                
                if (_parameterRefs[index].Equals(key))
                {
                    value = _parameterRefs[index].ParameterInfo;
                    _lastAccessIndex = index; // Update for next locality-aware search
                    return true;
                }
            }

            value = default!;
            return false;
        }

        public IEnumerable<ParameterInfo> Values => 
            _fallbackDict?.Values ?? _parameterRefs.Select(r => r.ParameterInfo);

        /// <summary>
        /// Converts the linear search array to a fallback dictionary for large parameter sets.
        /// </summary>
        private void ConvertToFallbackDictionary()
        {
#if NETSTANDARD2_0
            _fallbackDict = new Dictionary<Key, ParameterInfo>();
            
            // We need to reconstruct Key objects for the dictionary
            // This is less efficient but maintains compatibility
            foreach (var paramRef in _parameterRefs)
            {
                // Create a temporary key from the parameter name for dictionary storage
                var parameterName = GetParameterName(paramRef.ParameterInfo);
                var utf8Bytes = System.Text.Encoding.UTF8.GetBytes(parameterName);
                
                // Create a memory buffer to construct a Key
                var buffer = new ArrayBuffer(utf8Bytes);
                var tempKey = new Key(buffer, 0, utf8Bytes.Length);
                
                _fallbackDict.Add(tempKey, paramRef.ParameterInfo);
            }
#else
            // For newer frameworks, we can be more efficient
            _fallbackDict = new Dictionary<Key, ParameterInfo>();
            
            foreach (var paramRef in _parameterRefs)
            {
                var parameterName = GetParameterName(paramRef.ParameterInfo);
                var utf8Bytes = System.Text.Encoding.UTF8.GetBytes(parameterName);
                var buffer = new ArrayBuffer(utf8Bytes);
                var tempKey = new Key(buffer, 0, utf8Bytes.Length);
                
                _fallbackDict.Add(tempKey, paramRef.ParameterInfo);
            }
#endif
            
            // Clear the array to free memory
            _parameterRefs = Array.Empty<ParameterRef>();
            _lastAccessIndex = 0;
        }

        /// <summary>
        /// Extracts parameter name from a ParameterInfo object.
        /// Looks for ParameterAttribute first, falls back to parameter name.
        /// </summary>
        private static string GetParameterName(ParameterInfo parameterInfo)
        {
            // Check for ParameterAttribute to get the database parameter name
            var paramAttribute = parameterInfo.GetCustomAttribute<ParameterAttribute>();
            return paramAttribute?.ParameterName ?? parameterInfo.Name ?? "unknown";
        }

        /// <summary>
        /// Attempts to extract the parameter name from a Key.
        /// This is a bridge method to work with the existing Key-based API.
        /// </summary>
        private static string ExtractParameterName(Key key, ParameterInfo parameterInfo)
        {
            // Try to get the parameter name from the ParameterInfo first
            string parameterName = GetParameterName(parameterInfo);
            
            // Validate by checking if the UTF-8 encoded name matches the key
            var utf8Bytes = System.Text.Encoding.UTF8.GetBytes(parameterName);
            var reconstructedBuffer = new ArrayBuffer(utf8Bytes);
            var reconstructedKey = new Key(reconstructedBuffer, 0, utf8Bytes.Length);
            
            if (reconstructedKey.Equals(key))
            {
                return parameterName;
            }

            // If validation fails, try to extract from the key directly
            // This is more expensive but handles edge cases
            return ExtractParameterNameFromKey(key) ?? parameterName;
        }

        /// <summary>
        /// Attempts to extract the parameter name string from a Key object.
        /// This is a fallback method for cases where parameter name reconstruction fails.
        /// </summary>
        private static string? ExtractParameterNameFromKey(Key key)
        {
            try
            {
                // Use reflection to access Key's private fields
                var keyType = typeof(Key);
                var bufferField = keyType.GetField("buffer", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var offsetField = keyType.GetField("offset", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var sizeField = keyType.GetField("size", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (bufferField?.GetValue(key) is not Buffer buffer ||
                    offsetField?.GetValue(key) is not long offset ||
                    sizeField?.GetValue(key) is not int size)
                    return null;

                // Extract bytes and convert to string
                var bytes = new byte[size];
                for (int i = 0; i < size; i++)
                {
                    bytes[i] = buffer.ReadOne(offset + i);
                }

                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null; // Return null if extraction fails
            }
        }
    }
}