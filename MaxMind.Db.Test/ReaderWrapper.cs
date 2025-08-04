using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace MaxMind.Db.Test
{
    /// <summary>
    /// Abstract wrapper around Reader that allows testing both source generator and reflection paths
    /// </summary>
    public abstract class ReaderWrapper : IDisposable
    {
        /// <summary>
        /// Indicates whether this wrapper uses source-generated activators
        /// </summary>
        public abstract bool IsSourceGeneratedReader { get; }

        /// <summary>
        /// Create a reader for the specified database file
        /// </summary>
        /// <param name="file">Path to the database file</param>
        /// <returns>Reader instance</returns>
        public abstract Reader CreateReader(string file);

        /// <summary>
        /// Create a reader for the specified database stream
        /// </summary>
        /// <param name="stream">Database stream</param>
        /// <returns>Reader instance</returns>
        public abstract Reader CreateReader(Stream stream);

        /// <summary>
        /// Find a record and deserialize it to the specified type
        /// </summary>
        /// <typeparam name="T">Type to deserialize to</typeparam>
        /// <param name="reader">Reader instance</param>
        /// <param name="ipAddress">IP address to look up</param>
        /// <returns>Deserialized record</returns>
        public abstract T? Find<T>(Reader reader, IPAddress ipAddress) where T : class;

        /// <summary>
        /// Find a record and deserialize it to the specified type with injectable parameters
        /// </summary>
        /// <typeparam name="T">Type to deserialize to</typeparam>
        /// <param name="reader">Reader instance</param>
        /// <param name="ipAddress">IP address to look up</param>
        /// <param name="injectables">Injectable parameters</param>
        /// <returns>Deserialized record</returns>
        public abstract T? Find<T>(Reader reader, IPAddress ipAddress, InjectableValues? injectables) where T : class;

        protected virtual void Dispose(bool disposing)
        {
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Reader wrapper that uses reflection-based activation
    /// </summary>
    public class ReflectionReaderWrapper : ReaderWrapper
    {
        public override bool IsSourceGeneratedReader => false;

        public override Reader CreateReader(string file)
        {
            // Force reflection mode by disabling source generator support
            return new Reader(file);
        }

        public override Reader CreateReader(Stream stream)
        {
            return new Reader(stream);
        }

        public override T? Find<T>(Reader reader, IPAddress ipAddress) where T : class
        {
            // Temporarily disable source generator to force reflection
            return DisableSourceGenerators(() => reader.Find<T>(ipAddress));
        }

        public override T? Find<T>(Reader reader, IPAddress ipAddress, InjectableValues? injectables) where T : class
        {
            return DisableSourceGenerators(() => reader.Find<T>(ipAddress, injectables));
        }

        private static T DisableSourceGenerators<T>(Func<T> operation)
        {
#if NET8_0_OR_GREATER && DEBUG
            // Temporarily disable source generators for this thread only
            using var disabler = SourceGeneratorSupport.DisableSourceGeneratorsForCurrentThread();
            return operation();
#else
            return operation();
#endif
        }
    }

    /// <summary>
    /// Reader wrapper that uses source-generated activators
    /// </summary>
    public class SourceGeneratorReaderWrapper : ReaderWrapper
    {
        public override bool IsSourceGeneratedReader => true;

        public override Reader CreateReader(string file)
        {
            return new Reader(file);
        }

        public override Reader CreateReader(Stream stream)
        {
            return new Reader(stream);
        }

        public override T? Find<T>(Reader reader, IPAddress ipAddress) where T : class
        {
            return reader.Find<T>(ipAddress);
        }

        public override T? Find<T>(Reader reader, IPAddress ipAddress, InjectableValues? injectables) where T : class
        {
            return reader.Find<T>(ipAddress, injectables);
        }
    }
}