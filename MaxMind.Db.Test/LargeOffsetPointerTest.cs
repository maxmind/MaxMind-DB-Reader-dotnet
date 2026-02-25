#region

using System;
using Xunit;

#endregion

namespace MaxMind.Db.Test
{
    public class LargeOffsetPointerTest
    {
        /// <summary>
        ///     Verify that the decoder correctly follows pointers when the
        ///     resolved offset exceeds <see cref="int.MaxValue" />. This
        ///     exercises the fix for GitHub #263 where
        ///     <c>Convert.ToInt32(pointer)</c> threw
        ///     <see cref="OverflowException" /> for databases larger than
        ///     2 GiB.
        /// </summary>
        [Fact]
        public void TestPointerDecodingWithLargeOffset()
        {
            // Target string "test" encoded as MaxMind DB UTF-8 string:
            // 0x44 = type 2 (UTF-8 string) in top 3 bits, size 4 in bottom 5 bits
            var targetData = new byte[] { 0x44, 0x74, 0x65, 0x73, 0x74 };

            // Pointer record: size 0 pointer (11-bit) pointing to packed value 0.
            // 0x20 = type 1 (pointer) in top 3 bits, size bits = 00 000
            // 0x00 = next byte, combined with VVV bits gives pointer value 0.
            var pointerData = new byte[] { 0x20, 0x00 };

            // Layout: [pointer @ offset 0] [target string @ offset 2]
            var raw = new byte[pointerData.Length + targetData.Length];
            Array.Copy(pointerData, 0, raw, 0, pointerData.Length);
            Array.Copy(targetData, 0, raw, pointerData.Length, targetData.Length);

            // DecodePointer computes: packed + pointerBase + pointerValueOffset[1]
            //   packed = 0, pointerValueOffset[1] = 0, so resolved = pointerBase.
            // We want the resolved offset to point to the target string at
            // inner buffer offset 2, which the OffsetBuffer maps to
            // baseOffset + 2.
            var baseOffset = (long)int.MaxValue + 1;
            var pointerBase = baseOffset + pointerData.Length;

            var innerBuffer = new ArrayBuffer(raw);
            using var offsetBuffer = new OffsetBuffer(innerBuffer, baseOffset);
            var decoder = new Decoder(offsetBuffer, pointerBase);

            // Before the fix, this threw OverflowException because the
            // resolved pointer (> int.MaxValue) was passed through
            // Convert.ToInt32().
            var result = decoder.Decode<string>(baseOffset, out _);
            Assert.Equal("test", result);
        }

        /// <summary>
        ///     A Buffer wrapper that simulates large file offsets by adding a
        ///     base offset to all reads. This allows testing decoder behavior
        ///     with offsets exceeding <see cref="int.MaxValue" /> without
        ///     requiring a multi-GiB file.
        /// </summary>
        private sealed class OffsetBuffer : Buffer
        {
            private readonly Buffer _inner;
            private readonly long _baseOffset;

            public OffsetBuffer(Buffer inner, long baseOffset)
            {
                _inner = inner;
                _baseOffset = baseOffset;
                Length = inner.Length + baseOffset;
            }

            public override byte[] Read(long offset, int count)
            {
                return _inner.Read(offset - _baseOffset, count);
            }

            public override byte ReadOne(long offset)
            {
                return _inner.ReadOne(offset - _baseOffset);
            }

            public override string ReadString(long offset, int count)
            {
                return _inner.ReadString(offset - _baseOffset, count);
            }

            public override int ReadInt(long offset)
            {
                return _inner.ReadInt(offset - _baseOffset);
            }

            public override int ReadVarInt(long offset, int count)
            {
                return _inner.ReadVarInt(offset - _baseOffset, count);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _inner.Dispose();
                }
            }
        }
    }
}
