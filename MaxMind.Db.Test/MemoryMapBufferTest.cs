using System.IO;
using Xunit;

namespace MaxMind.Db.Test
{
    public static class MemoryMapBufferTest
    {
        [Theory]
        [InlineData(0, 5)]
        [InlineData(3, 2)]
        [InlineData(-1, 1)]
        [InlineData(long.MaxValue, 1)]
        public static void ReadsRejectOutOfBoundsRanges(long offset, int count)
        {
            using var buffer = new MemoryMapBuffer(new MemoryStream(new byte[4], writable: false));
            using var other = new MemoryMapBuffer(new MemoryStream(new byte[8], writable: false));
            Assert.Throws<InvalidDatabaseException>(() => buffer.HashBytes(offset, count));
            Assert.Throws<InvalidDatabaseException>(() => buffer.ReadLong(offset, count));
            Assert.Throws<InvalidDatabaseException>(() => buffer.ReadULong(offset, count));
            Assert.Throws<InvalidDatabaseException>(() => buffer.EqualsBytes(offset, new byte[8], 0, count));
            // Both operands must be checked, even when all bytes compare equal.
            Assert.Throws<InvalidDatabaseException>(() => buffer.EqualsBytes(offset, other, 0, count));
            Assert.Throws<InvalidDatabaseException>(() => other.EqualsBytes(0, buffer, offset, count));
        }

        [Fact]
        public static void ReadsAcceptRangesEndingAtTheBufferBoundary()
        {
            using var buffer = new MemoryMapBuffer(new MemoryStream([0, 1, 2, 3], writable: false));
            using var other = new MemoryMapBuffer(new MemoryStream([1, 2, 3], writable: false));
            Assert.Equal(0x010203, buffer.ReadLong(1, 3));
            Assert.Equal(0x010203UL, buffer.ReadULong(1, 3));
            Assert.Equal(other.HashBytes(0, 3), buffer.HashBytes(1, 3));
            Assert.True(buffer.EqualsBytes(1, other, 0, 3));
            Assert.True(buffer.EqualsBytes(1, new byte[] { 1, 2, 3 }, 0, 3));
            Assert.False(buffer.EqualsBytes(1, new byte[] { 1, 2, 4 }, 0, 3));
        }
    }
}
