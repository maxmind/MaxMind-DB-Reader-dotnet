#region

using Xunit;

#endregion

namespace MaxMind.Db.Test
{
    public class Unsigned32BitRecordTest
    {
        /// <summary>
        ///     Verify that a 32-bit search tree record with the high bit set
        ///     is correctly interpreted as an unsigned value. This exercises the
        ///     <c>(uint)ReadInt</c> cast in <c>Reader.ReadNode</c> for the
        ///     32-bit record case.
        /// </summary>
        /// <remarks>
        ///     An end-to-end test through <see cref="Reader" /> is not feasible
        ///     because a 32-bit record value with the high bit set implies a
        ///     resolved data offset exceeding 2 GiB, which would require an
        ///     impractically large test database. Instead, we test the cast at
        ///     the buffer level, which is the exact operation performed in
        ///     <c>ReadNode</c>.
        /// </remarks>
        [Theory]
        [InlineData(new byte[] { 0x80, 0x00, 0x00, 0x00 }, 2147483648L)]
        [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, 4294967295L)]
        [InlineData(new byte[] { 0x80, 0x00, 0x00, 0x03 }, 2147483651L)]
        [InlineData(new byte[] { 0x7F, 0xFF, 0xFF, 0xFF }, 2147483647L)]
        public void TestReadIntCastToUintProducesUnsignedValue(byte[] data, long expected)
        {
            using var buffer = new ArrayBuffer(data);

            // ReadInt returns a signed int. Casting through uint reinterprets
            // the sign bit as a value bit, and the implicit uint -> long
            // widening preserves the unsigned value. This is the pattern used
            // in Reader.ReadNode for 32-bit records.
            var result = (long)(uint)buffer.ReadInt(0);

            Assert.Equal(expected, result);
        }
    }
}
