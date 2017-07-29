using System;
using System.IO;

namespace MaxMind.Db.Test.Helper
{
    internal class NonSeekableStreamWrapper
        : Stream
    {
        private readonly Stream _wrappedStream;

        public NonSeekableStreamWrapper(Stream wrappedStream)
        {
            _wrappedStream = wrappedStream;
        }
        
        public override void Flush()
        {
            _wrappedStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _wrappedStream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _wrappedStream.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            _wrappedStream.Dispose();
        }

        public override bool CanRead => _wrappedStream.CanRead;
        public override bool CanSeek =>false;
        public override bool CanWrite => _wrappedStream.CanWrite;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }
}