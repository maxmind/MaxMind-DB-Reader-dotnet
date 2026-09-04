#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using Xunit;

#endregion

namespace MaxMind.Db.Test
{
    public static class DecoderTest
    {
        [Theory]
        [MemberData(nameof(TestUInt16))]
        [MemberData(nameof(TestUInt32))]
        [MemberData(nameof(TestInt32s))]
        [MemberData(nameof(TestUInt64s))]
        [MemberData(nameof(TestBigIntegers))]
        [MemberData(nameof(TestDoubles))]
        [MemberData(nameof(TestFloats))]
        [MemberData(nameof(TestPointers))]
        [MemberData(nameof(TestStrings))]
        [MemberData(nameof(TestBooleans))]
        [MemberData(nameof(TestBytes))]
        [MemberData(nameof(TestMaps))]
        [MemberData(nameof(TestArrays))]
        public static void TestTypeDecoding<T>(Dictionary<T, byte[]> tests) where T : class
        {
            foreach (var entry in tests)
            {
                var expect = entry.Key;
                var input = entry.Value;

                using var database = new MemoryMapBuffer(new MemoryStream(input, writable: false));
                var decoder = new Decoder(database, 0, false);
                var val = decoder.Decode<T>(0, out _);
                Assert.Equal(expect, val);
            }
        }

        private static void WritePointer1(List<byte> bytes, int target)
        {
            // One-byte-payload pointer (type 1, pointer_size 1) with base 0.
            bytes.Add((byte)((1 << 5) | ((target >> 8) & 0x7)));
            bytes.Add((byte)(target & 0xFF));
        }

        private static byte[] NestedContainers(int count)
        {
            var bytes = new List<byte>(count * 3 + 1);
            for (var i = 0; i < count; i++)
            {
                if (i % 2 == 0)
                {
                    bytes.Add(0x01); // array with one element
                    bytes.Add(0x04);
                }
                else
                {
                    bytes.Add(0xE1); // map with one entry
                    bytes.Add(0x41); // one-byte string key
                    bytes.Add((byte)'x');
                }
            }
            bytes.Add(0xA0); // leaf: uint16 with value 0
            return [.. bytes];
        }

        [Fact]
        public static void TestPointerFanOutIsBounded()
        {
            // A data section of nested arrays, each holding two pointers to the
            // node below, would cost 2**depth decode operations. The decoder
            // bounds the number of values it decodes per lookup and rejects the
            // database.
            const int depth = 100;
            var bytes = new List<byte> { 0xA0 }; // leaf: uint16 with value 0
            var prev = 0;
            for (var i = 0; i < depth; i++)
            {
                var offset = bytes.Count;
                bytes.Add(0x02);
                bytes.Add(0x04);
                WritePointer1(bytes, prev);
                WritePointer1(bytes, prev);
                prev = offset;
            }

            using var database = new MemoryMapBuffer(new MemoryStream(bytes.ToArray(), writable: false));
            var decoder = new Decoder(database, 0);
            var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(prev, out _));
            Assert.Contains("maximum number of values", ex.Message);
        }

        [Fact]
        public static void TestMapPointerFanOutIsBounded()
        {
            // Each map has two distinct keys whose values point to the map
            // below. Re-decoding the shared targets must consume the map's two
            // key/value pairs from the value budget on every visit.
            const int depth = 100;
            var bytes = new List<byte> { 0xA0 }; // leaf: uint16 with value 0
            var prev = 0;
            for (var i = 0; i < depth; i++)
            {
                var offset = bytes.Count;
                bytes.Add(0xE2);
                bytes.Add(0x41);
                bytes.Add((byte)'a');
                WritePointer1(bytes, prev);
                bytes.Add(0x41);
                bytes.Add((byte)'b');
                WritePointer1(bytes, prev);
                prev = offset;
            }

            using var database = new MemoryMapBuffer(new MemoryStream(bytes.ToArray(), writable: false));
            var decoder = new Decoder(database, 0);
            var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(prev, out _));
            Assert.Contains("maximum number of values", ex.Message);
        }

        // 256 bytes of string payload per pointer target. 8,192 occurrences
        // reach exactly the 2 MiB payload budget and 8,193 cross it.
        private const int FlatFanOutTargetSize = 256;

        // A 256-byte string target followed by an array of pointers, all
        // pointing at that one target. Many pointers to one scalar is a flat
        // fan-out: the value budget cannot bound it, because the array
        // charges each pointer once and following a pointer adds no value of
        // its own. The payload budget bounds it instead, since it charges the
        // target length on every occurrence.
        private static byte[] FlatScalarPointerTargets(int pointerCount, out int arrayOffset)
        {
            var encodedSize = pointerCount - 285;
            var bytes = new List<byte>(pointerCount * 2 + FlatFanOutTargetSize + 8)
            {
                0x5D, // target: UTF-8 string with a one-byte encoded size
                (byte)(FlatFanOutTargetSize - 29),
            };
            bytes.AddRange(new byte[FlatFanOutTargetSize]);
            arrayOffset = bytes.Count;
            bytes.Add(0x1E);
            bytes.Add(0x04); // array with a two-byte encoded size
            bytes.Add((byte)(encodedSize >> 8));
            bytes.Add((byte)encodedSize);
            for (var i = 0; i < pointerCount; i++)
            {
                WritePointer1(bytes, 0);
            }

            return [.. bytes];
        }

        [Theory]
        [InlineData(8_192, false)]
        [InlineData(8_193, true)]
        public static void TestFlatScalarPointerTargetsConsumePayloadBudget(int pointerCount, bool exceedsLimit)
        {
            // This is intentionally flat so neither depth nor exponential
            // container fan-out can hide incorrect payload accounting.
            var bytes = FlatScalarPointerTargets(pointerCount, out var arrayOffset);

            using var database = new MemoryMapBuffer(new MemoryStream(bytes, writable: false));
            var decoder = new Decoder(database, 0);
            if (exceedsLimit)
            {
                var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(arrayOffset, out _));
                Assert.Equal(
                    "The MaxMind DB file's data section exceeds the maximum payload size.",
                    ex.Message);
            }
            else
            {
                var decoded = Assert.IsType<List<object>>(decoder.Decode<object>(arrayOffset, out var offset));
                Assert.Equal(pointerCount, decoded.Count);
                Assert.Equal(bytes.Length, offset);
            }
        }

        [Theory]
        [InlineData(8_192, false)]
        [InlineData(8_193, true)]
        public static void TestFlatModelKeyPointerTargetsConsumePayloadBudget(int pointerCount, bool exceedsLimit)
        {
            // The same flat fan-out through map keys, which DecodeKey reads on a
            // separate path. A key is hashed over its bytes on every visit, so
            // the payload budget must charge each visit. The keys are unknown to
            // KeyOnlyModel, so their false values are skipped without
            // introducing another pointer path.
            var encodedSize = pointerCount - 285;
            var bytes = new List<byte>(pointerCount * 4 + FlatFanOutTargetSize + 8)
            {
                0x5D, // target: UTF-8 string with a one-byte encoded size
                (byte)(FlatFanOutTargetSize - 29),
            };
            bytes.AddRange(new byte[FlatFanOutTargetSize]);
            var mapOffset = bytes.Count;
            bytes.Add(0xFE); // map with a two-byte encoded size
            bytes.Add((byte)(encodedSize >> 8));
            bytes.Add((byte)encodedSize);
            for (var i = 0; i < pointerCount; i++)
            {
                WritePointer1(bytes, 0);
                bytes.Add(0x00); // extended boolean
                bytes.Add(0x07); // false
            }

            using var database = new MemoryMapBuffer(new MemoryStream(bytes.ToArray(), writable: false));
            var decoder = new Decoder(database, 0);
            if (exceedsLimit)
            {
                var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<KeyOnlyModel>(mapOffset, out _));
                Assert.Equal(
                    "The MaxMind DB file's data section exceeds the maximum payload size.",
                    ex.Message);
            }
            else
            {
                var decoded = decoder.Decode<KeyOnlyModel>(mapOffset, out var offset);
                Assert.Null(decoded.Name);
                Assert.Equal(bytes.Count, offset);
            }
        }

        // An array of 65,535 booleans. Decode charges one value for the root,
        // so MaxDecodedValues - 1 = 65,535 remain for children. A container
        // declaring exactly that many children lands the value budget at
        // zero and is accepted. The elements are booleans, the cheapest
        // scalar, so this isolates the value budget from the payload budget.
        private static byte[] AtValueBudgetLimitArray()
        {
            const int childCount = 65_535;
            var encodedSize = childCount - 285;
            var bytes = new List<byte>(childCount * 2 + 4)
            {
                0x1E, // array with a two-byte encoded size
                0x04,
                (byte)(encodedSize >> 8),
                (byte)encodedSize,
            };
            for (var i = 0; i < childCount; i++)
            {
                bytes.Add(0x00); // extended boolean
                bytes.Add(0x07); // false
            }

            return [.. bytes];
        }

        [Fact]
        public static void TestRepeatedAtValueBudgetLimitDecodesSucceed()
        {
            // The budget is a ref parameter threaded through Decode, not a
            // field on Decoder. Decoding the same at-limit record three times
            // from one Decoder must succeed all three times. It fails on the
            // second decode if the budget lived on the Decoder instead.
            const int childCount = 65_535;
            var bytes = AtValueBudgetLimitArray();
            using var database = new MemoryMapBuffer(new MemoryStream(bytes, writable: false));
            var decoder = new Decoder(database, 0);

            for (var i = 0; i < 3; i++)
            {
                var decoded = Assert.IsType<List<object>>(decoder.Decode<object>(0, out _));
                Assert.Equal(childCount, decoded.Count);
            }
        }

        [Fact]
        public static void TestRepeatedAtPayloadBudgetLimitDecodesSucceed()
        {
            // Same proof as above, for the payload budget: 8,192 pointers at
            // 256 bytes each lands the payload budget at exactly zero.
            const int pointerCount = 8_192;
            var bytes = FlatScalarPointerTargets(pointerCount, out var arrayOffset);
            using var database = new MemoryMapBuffer(new MemoryStream(bytes, writable: false));
            var decoder = new Decoder(database, 0);

            for (var i = 0; i < 3; i++)
            {
                var decoded = Assert.IsType<List<object>>(decoder.Decode<object>(arrayOffset, out _));
                Assert.Equal(pointerCount, decoded.Count);
            }
        }

        [Fact]
        public static void TestManySimultaneousAtBudgetLimitDecodesSucceed()
        {
            // A smoke test, not a race detector: Parallel.For does not
            // guarantee real thread overlap, so this alone cannot prove
            // thread safety. The actual regression lock against a budget
            // hoisted onto Decoder is the pair of repeated tests above,
            // since a shared counter fails those on the second decode
            // regardless of threading. This test only checks that many
            // simultaneous at-limit decodes through one shared Decoder all
            // still succeed. ThreadingTest.cs carries this reader's real
            // concurrency coverage (TestParallelFor, TestManyOpens).
            const int pointerCount = 8_192;
            const int childCount = 65_535;
            var payloadBytes = FlatScalarPointerTargets(pointerCount, out var arrayOffset);
            var valueBytes = AtValueBudgetLimitArray();
            var valueOffset = payloadBytes.Length;
            var bytes = new byte[payloadBytes.Length + valueBytes.Length];
            payloadBytes.CopyTo(bytes, 0);
            valueBytes.CopyTo(bytes, valueOffset);

            using var database = new MemoryMapBuffer(new MemoryStream(bytes, writable: false));
            var decoder = new Decoder(database, 0);

            System.Threading.Tasks.Parallel.For(0, 16, i =>
            {
                if (i % 2 == 0)
                {
                    var decoded = Assert.IsType<List<object>>(decoder.Decode<object>(arrayOffset, out _));
                    Assert.Equal(pointerCount, decoded.Count);
                }
                else
                {
                    var decoded = Assert.IsType<List<object>>(decoder.Decode<object>(valueOffset, out _));
                    Assert.Equal(childCount, decoded.Count);
                }
            });
        }

        [Theory]
        [InlineData(32, false)]
        [InlineData(33, false)]
        [InlineData(514, true)]
        public static void TestContainerDepthIsBounded(int containerCount, bool exceedsLimit)
        {
            // Each container level consumes several managed stack frames. The
            // available stack varies by runtime, so do not require all 512
            // format-level depths to fit. The decoder must reject the corrupt
            // case with a catchable exception before the runtime terminates the
            // process. Alternating maps and arrays exercises depth propagation
            // through both paths.
            var bytes = NestedContainers(containerCount);
            using var database = new MemoryMapBuffer(new MemoryStream(bytes, writable: false));
            var decoder = new Decoder(database, 0);

            if (exceedsLimit)
            {
                var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(0, out _));
                Assert.Equal("The MaxMind DB file's data section exceeds the maximum depth.", ex.Message);
            }
            else
            {
                decoder.Decode<object>(0, out var offset);
                Assert.Equal(bytes.Length, offset);
            }
        }

        [Fact]
        public static void TestContainerDepthBoundaryRejectsOneOverTheLimit()
        {
            // Pins the corrected format-level boundary from the tight side:
            // 513 nested containers must reject. Commit b254329 on this
            // branch deliberately dropped a matching 512-container accept row
            // from TestContainerDepthIsBounded (513 down to 32/33) after a
            // deep container decode terminated the .NET 8 test host on macOS
            // and Windows with an uncatchable StackOverflowException. Do not
            // re-add a 512-container accept row here.
            // TestContainerDepthAtLimitSucceedsGivenSufficientStack below
            // pins the accept side instead, on a thread with room to spare so
            // it cannot repeat that crash. A rejection here cannot overflow
            // the stack: the guard fires before the recursion that would
            // grow it.
            var bytes = NestedContainers(513);
            using var database = new MemoryMapBuffer(new MemoryStream(bytes, writable: false));
            var decoder = new Decoder(database, 0);

            var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(0, out _));
            Assert.Equal("The MaxMind DB file's data section exceeds the maximum depth.", ex.Message);
        }

        [Fact]
        public static void TestContainerDepthAtLimitSucceedsGivenSufficientStack()
        {
            // FINDING 1 of the final whole-branch review: nothing proved that
            // exactly 512 nested containers -- the format's accepted depth
            // boundary -- can actually be decoded. b254329 added the
            // HasSufficientExecutionStack probe and, in the same commit,
            // removed a 512-container accept row, because a deep container
            // decode had already killed the .NET 8 test host with an
            // uncatchable StackOverflowException on macOS and Windows. That
            // left the probe itself unverified at the boundary it exists to
            // guard. lessons.md's depth section requires that 512 valid
            // logical levels decode safely, or that the decoder become
            // iterative.
            //
            // This test proves the accept side on a thread given 16 MiB of
            // stack, far more than 512 container levels need, so it cannot
            // reproduce the crash b254329 was written to avoid. The
            // companion test below,
            // TestContainerDepthAtLimitDoesNotCrashTheHostOnADefaultStack,
            // proves the reject-or-succeed side on whatever stack the test
            // host actually gives it. Together they show the limit is
            // reachable when the stack allows it, and the probe converts a
            // stack shortage into a catchable database error rather than
            // terminating the process. Neither branch can crash a host: a
            // rejection happens before the recursion grows, and this
            // large-stack thread has room to spare.
            var bytes = NestedContainers(512);
            Exception? failure = null;

            var thread = new Thread(() =>
            {
                try
                {
                    using var database = new MemoryMapBuffer(new MemoryStream(bytes, writable: false));
                    var decoder = new Decoder(database, 0);
                    decoder.Decode<object>(0, out var offset);
                    Assert.Equal(bytes.Length, offset);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            }, maxStackSize: 16 << 20);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        [Fact]
        public static void TestContainerDepthAtLimitDoesNotCrashTheHostOnADefaultStack()
        {
            // Companion to TestContainerDepthAtLimitSucceedsGivenSufficientStack
            // above. On whatever stack this test happens to run on, decoding
            // exactly 512 nested containers must either succeed or fail with
            // the decoder's own catchable InvalidDatabaseException, never an
            // uncatchable StackOverflowException. That is what
            // HasSufficientExecutionStack (added in b254329) exists to
            // guarantee: it turns a stack shortage into a database error
            // instead of terminating the process.
            //
            // Do not change this to require success. Requiring 512 containers
            // to decode on a default stack is exactly the case b254329
            // removed after it crashed the .NET 8 test host on macOS and
            // Windows.
            var bytes = NestedContainers(512);
            using var database = new MemoryMapBuffer(new MemoryStream(bytes, writable: false));
            var decoder = new Decoder(database, 0);

            try
            {
                decoder.Decode<object>(0, out var offset);
                Assert.Equal(bytes.Length, offset);
            }
            catch (InvalidDatabaseException ex)
            {
                Assert.Equal("The MaxMind DB file's data section exceeds the maximum depth.", ex.Message);
            }
        }

        [Fact]
        public static void TestCyclicPointerThrows()
        {
            // A pointer to itself must throw a catchable InvalidDatabaseException
            // rather than recursing until the stack overflows. The cycle holds
            // no container, so nothing charges the value budget and the depth
            // guard is what stops it. That is still correct: each hop is a
            // fixed amount of work, so the depth limit bounds the total.
            using var database = new MemoryMapBuffer(new MemoryStream([0x20, 0x00], writable: false));
            var decoder = new Decoder(database, 0);
            var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(0, out _));
            Assert.Contains("maximum depth", ex.Message);
        }

        private sealed class KeyOnlyModel
        {
            [Constructor]
            public KeyOnlyModel([MapKey("name")] string? name = null) => Name = name;

            public string? Name { get; }
        }

        [Fact]
        public static void TestOversizedMapIsBounded()
        {
            // A map entry decodes a key and a value, so a map of N entries costs
            // 2N values. A map that declares 32,769 entries reaches 65,538
            // values, just past the 65,536 limit, and is rejected before any
            // entry is read. 0xfe is a map with size code 30, then the two size
            // bytes for 32,769 - 285 = 32,484 (0x7ee4).
            using var database = new MemoryMapBuffer(new MemoryStream([0xfe, 0x7e, 0xe4], writable: false));
            var decoder = new Decoder(database, 0);
            var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(0, out _));
            Assert.Contains("maximum number of values", ex.Message);
        }

        [Fact]
        public static void TestUnknownFieldValueCountIsBounded()
        {
            // The root map already charges its key and value. The unknown value
            // is a complete array whose 65,535 children exceed the remaining
            // budget. Skipping it must enforce the same limit as decoding it.
            const int childCount = 65_535;
            var bytes = new List<byte>(childCount * 2 + 16)
            {
                0xE1,
                0x47,
                (byte)'u', (byte)'n', (byte)'k', (byte)'n', (byte)'o', (byte)'w', (byte)'n',
                0x1E, 0x04, 0xFE, 0xE2,
            };
            for (var i = 0; i < childCount; i++)
            {
                bytes.Add(0x00); // extended boolean with value false
                bytes.Add(0x07);
            }

            using var database = new MemoryMapBuffer(new MemoryStream(bytes.ToArray(), writable: false));
            var decoder = new Decoder(database, 0);
            var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<KeyOnlyModel>(0, out _));
            Assert.Contains("maximum number of values", ex.Message);
        }

        [Fact]
        public static void TestUnknownFieldDepthIsBounded()
        {
            // The unknown map value begins at depth one. Its 512th nested
            // container therefore exceeds the maximum depth while being
            // skipped, without any pointers in the data.
            var nested = NestedContainers(513);
            var bytes = new List<byte>(nested.Length + 9)
            {
                0xE1,
                0x47,
                (byte)'u', (byte)'n', (byte)'k', (byte)'n', (byte)'o', (byte)'w', (byte)'n',
            };
            bytes.AddRange(nested);

            using var database = new MemoryMapBuffer(new MemoryStream(bytes.ToArray(), writable: false));
            var decoder = new Decoder(database, 0);
            var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<KeyOnlyModel>(0, out _));
            Assert.Contains("maximum depth", ex.Message);
        }

        [Fact]
        public static void TestCyclicPointerAsMapKeyThrows()
        {
            // Decoding into a model type reads map keys through a separate path
            // (DecodeKey) from the dictionary path. A key that is a pointer to
            // itself must also throw a catchable InvalidDatabaseException rather
            // than overflowing the stack.
            // The map charges its one key and value, then the key cycle holds no
            // container, so the depth guard stops it. Each hop is a fixed
            // amount of work, so the depth limit bounds the total.
            // 0xe1: map with one entry. The key at offset 1 is a one-byte
            // pointer (0x20 0x01) whose target is offset 1, the pointer itself.
            using var database = new MemoryMapBuffer(new MemoryStream([0xe1, 0x20, 0x01], writable: false));
            var decoder = new Decoder(database, 0);
            var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<KeyOnlyModel>(0, out _));
            Assert.Contains("maximum depth", ex.Message);
        }

        // Ordering proofs. Each buffer holds only a header that declares more
        // than the limit allows, with no body. If the guard runs before the
        // read or the loop, the limit exception is thrown. If it runs after,
        // the decoder reads past the end and reports truncation instead. The
        // message discriminates the two, so these fail if a check ever moves
        // below the read or the loop it protects.

        [Fact]
        public static void TestOversizedArrayIsRejectedBeforeFirstChild()
        {
            // The root value costs one against the budget, leaving 65,535 for
            // an array's declared children. 0x1e is an extended type with size
            // code 30; 0x04 selects array (11 - 7). The two size bytes encode
            // 65,536 - 285 = 65,251 (0xfee3), one child past what remains.
            using var database = new MemoryMapBuffer(new MemoryStream([0x1e, 0x04, 0xfe, 0xe3], writable: false));
            var decoder = new Decoder(database, 0);
            var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(0, out _));
            Assert.Contains("maximum number of values", ex.Message);
        }

        [Fact]
        public static void TestOversizedStringIsRejectedBeforeItIsRead()
        {
            // A string declaring one byte past the 2 MiB payload budget, with
            // no payload following. 0x5f is a string with size code 31; the
            // three size bytes encode 2,097,153 - 65,821 = 2,031,332
            // (0x1efee4).
            using var database = new MemoryMapBuffer(
                new MemoryStream([0x5f, 0x1e, 0xfe, 0xe4], writable: false));
            var decoder = new Decoder(database, 0);
            var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(0, out _));
            Assert.Contains("maximum payload size", ex.Message);
        }

        [Fact]
        public static void TestOversizedBytesIsRejectedBeforeItIsRead()
        {
            // The same shape as the oversized string, as bytes. Bytes (4) is a
            // direct type, so no extended type byte is needed: 0x9f is bytes
            // with size code 31, followed by the same three size bytes
            // declaring 2,097,153 bytes.
            using var database = new MemoryMapBuffer(
                new MemoryStream([0x9f, 0x1e, 0xfe, 0xe4], writable: false));
            var decoder = new Decoder(database, 0);
            var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(0, out _));
            Assert.Contains("maximum payload size", ex.Message);
        }

        [Fact]
        public static void TestTruncatedPayloadThrowsDatabaseException()
        {
            // A string header declaring four bytes at the end of the buffer.
            // Truncated data is malformed input, so it must surface as the
            // reader's database exception rather than an argument exception.
            using var database = new MemoryMapBuffer(new MemoryStream([0x44], writable: false));
            var decoder = new Decoder(database, 0);
            var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(0, out _));
            Assert.Contains("beyond the end", ex.Message);
        }

        [Fact]
        public static void TestBufferReadRejectsOutOfBoundsOffsets()
        {
            // MemoryMapBuffer's netstandard2.0 read paths and its modern
            // GetSpan path share one bounds check. This exercises it
            // directly through Read rather than through the decoder.
            using var database = new MemoryMapBuffer(new MemoryStream([0x01, 0x02, 0x03, 0x04], writable: false));

            // A read that ends exactly at Length is accepted.
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, database.Read(0, 4));

            // A read that ends one byte past Length is rejected.
            var pastEnd = Assert.Throws<InvalidDatabaseException>(() => database.Read(0, 5));
            Assert.Contains("beyond the end", pastEnd.Message);

            // A negative offset is rejected.
            var negativeOffset = Assert.Throws<InvalidDatabaseException>(() => database.Read(-1, 1));
            Assert.Contains("beyond the end", negativeOffset.Message);
        }

        private static byte[] PointerChain(int length)
        {
            // A chain of one-byte-payload pointers ending in a uint16 leaf.
            // Link i sits at offset 2*i and points at offset 2*(i+1), so the
            // leaf lands at offset 2*length. A pointer follow no longer
            // charges the value budget, so each follow costs one depth
            // level and no value.
            var bytes = new List<byte>(length * 2 + 1);
            for (var i = 0; i < length; i++)
            {
                WritePointer1(bytes, 2 * (i + 1));
            }
            bytes.Add(0xA0); // leaf: uint16 with value 0
            return [.. bytes];
        }

        [Theory]
        [InlineData(511, false)]
        [InlineData(512, false)]
        [InlineData(513, true)]
        public static void TestPointerChainDepthIsBounded(int chainLength, bool exceedsLimit)
        {
            // Pointer follows guard depth on their own code path, separate
            // from the container path that TestContainerDepthIsBounded
            // covers. This test pins the pointer-chain boundary. A pointer
            // follow costs fewer stack frames than a container level, so the
            // exact boundary is reachable on this runtime. Verified: 512
            // links decode with no stack-probe rejection on .NET 10 on
            // Linux, the CI-representative runtime for this repo.
            //
            // The boundary sits at 512/513, matching MaxDepth. CheckDepth
            // guards the depth of the pointer being followed, not the depth
            // being entered. A chain of N links only checks depths 0
            // through N-1, and trips once that value reaches 512, at
            // N=513.
            var bytes = PointerChain(chainLength);
            using var database = new MemoryMapBuffer(new MemoryStream(bytes, writable: false));
            var decoder = new Decoder(database, 0);

            if (exceedsLimit)
            {
                var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(0, out _));
                Assert.Equal("The MaxMind DB file's data section exceeds the maximum depth.", ex.Message);
            }
            else
            {
                Assert.Equal(0, Assert.IsType<int>(decoder.Decode<object>(0, out _)));
            }
        }

        [Fact]
        public static void TestDepthAccumulatesAcrossContainerIntoPointerChain()
        {
            // The array's elements decode at depth one, inside the
            // container, not depth zero. A pointer chain followed from
            // there inherits that starting depth: this 600-link chain,
            // read through the array's first slot, rejects at the same
            // format-level depth TestPointerChainDepthIsBounded pins for a
            // bare chain, one link earlier than a chain starting at depth
            // zero would. The array's other two slots are never reached,
            // because the first slot's chain already throws. This does not
            // prove every slot independently exceeds the limit, only that
            // depth carries across the container-to-pointer boundary
            // instead of resetting.
            var chain = PointerChain(600);
            var arrayOffset = chain.Length;
            var bytes = new List<byte>(chain.Length + 8);
            bytes.AddRange(chain);
            bytes.Add(0x03); // extended type, size 3 (an array of 3 elements)
            bytes.Add(0x04); // extended type byte: array (11 - 7)
            WritePointer1(bytes, 0);
            WritePointer1(bytes, 0);
            WritePointer1(bytes, 0);

            using var database = new MemoryMapBuffer(new MemoryStream(bytes.ToArray(), writable: false));
            var decoder = new Decoder(database, 0);
            var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(arrayOffset, out _));
            Assert.Equal("The MaxMind DB file's data section exceeds the maximum depth.", ex.Message);
        }

        [Fact]
        public static void TestWideIntegerConsumesPayloadBudget()
        {
            // DecodeBigInteger calls ConsumePayload before ReadBigInteger
            // copies the declared bytes into a new array. An oversized
            // uint128 must be charged against the payload budget before the
            // copy. This declares a size one byte over the 2 MiB budget with
            // no body. An early charge reports the payload limit. A late
            // charge would instead read past the end and report truncation.
            // 0x1f selects the extended type with size code 31 (three size
            // bytes). 0x03 is the extended type byte for uint128
            // (ObjectType.Uint128 - 7). The size bytes encode
            // 2,097,153 - 65,821 = 2,031,332 (0x1efee4).
            using var database = new MemoryMapBuffer(
                new MemoryStream([0x1f, 0x03, 0x1e, 0xfe, 0xe4], writable: false));
            var decoder = new Decoder(database, 0);
            var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(0, out _));
            Assert.Contains("maximum payload size", ex.Message);
        }

        [Fact]
        public static void TestUint32ConsumesPayloadBudget()
        {
            // DecodeLong shares ConsumePayload's call pattern with
            // DecodeBigInteger: it charges the declared length before
            // ReadLong reads it. This is the same shape as
            // TestWideIntegerConsumesPayloadBudget, for a uint32 instead of
            // a uint128. Uint32 (type 6) is a direct type, so no extended
            // type byte is needed: 0xdf is uint32 with size code 31 (three
            // size bytes). The size bytes encode the same one-byte-over
            // length, 2,097,153 - 65,821 = 2,031,332 (0x1efee4).
            using var database = new MemoryMapBuffer(
                new MemoryStream([0xdf, 0x1e, 0xfe, 0xe4], writable: false));
            var decoder = new Decoder(database, 0);
            var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(0, out _));
            Assert.Contains("maximum payload size", ex.Message);
        }

        [Fact]
        public static void TestUint64ConsumesPayloadBudget()
        {
            // Same shape as TestWideIntegerConsumesPayloadBudget, for
            // DecodeUInt64. 0x1f selects the extended type with size code
            // 31 (three size bytes). 0x02 is the extended type byte for
            // uint64 (ObjectType.Uint64 - 7). The size bytes encode the
            // same one-byte-over length, 2,097,153 - 65,821 = 2,031,332
            // (0x1efee4).
            using var database = new MemoryMapBuffer(
                new MemoryStream([0x1f, 0x02, 0x1e, 0xfe, 0xe4], writable: false));
            var decoder = new Decoder(database, 0);
            var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(0, out _));
            Assert.Contains("maximum payload size", ex.Message);
        }

        public static IEnumerable<object[]> TestUInt16()
        {
            var uint16s = new Dictionary<object, byte[]>
            {
                {0, [0xa0] },
                {(1 << 8) - 1, [0xa1, 0xff] },
                {500, [0xa2, 0x1, 0xf4] },
                {10872, [0xa2, 0x2a, 0x78] },
                {(int) ushort.MaxValue, [0xa2, 0xff, 0xff] }
            };

            yield return [uint16s];
        }

        public static IEnumerable<object[]> TestUInt32()
        {
            var uint32s = new Dictionary<object, byte[]>
            {
                {0L, [0xc0] },
                {(1L << 8) - 1, [0xc1, 0xff] },
                {500L, [0xc2, 0x1, 0xf4] },
                {10872L, [0xc2, 0x2a, 0x78] },
                {(1L << 16) - 1, [0xc2, 0xff, 0xff] },
                {(1L << 24) - 1, [0xc3, 0xff, 0xff, 0xff] },
                {(long) uint.MaxValue, [0xc4, 0xff, 0xff, 0xff, 0xff] }
            };

            yield return [uint32s];
        }

        public static IEnumerable<object[]> TestInt32s()
        {
            var int32s = new Dictionary<object, byte[]>
            {
                {0, [0x0, 0x1] },
                {-1, [0x4, 0x1, 0xff, 0xff, 0xff, 0xff] },
                {(2 << 7) - 1, [0x1, 0x1, 0xff] },
                {1 - (2 << 7), [0x4, 0x1, 0xff, 0xff, 0xff, 0x1] },
                {500, [0x2, 0x1, 0x1, 0xf4] },
                {-500, [0x4, 0x1, 0xff, 0xff, 0xfe, 0xc] },
                {(2 << 15) - 1, [0x2, 0x1, 0xff, 0xff] },
                {1 - (2 << 15), [0x4, 0x1, 0xff, 0xff, 0x0, 0x1] },
                {(2 << 23) - 1, [0x3, 0x1, 0xff, 0xff, 0xff] },
                {1 - (2 << 23), [0x4, 0x1, 0xff, 0x0, 0x0, 0x1] },
                {int.MaxValue, [0x4, 0x1, 0x7f, 0xff, 0xff, 0xff] },
                {-int.MaxValue, [0x4, 0x1, 0x80, 0x0, 0x0, 0x1] }
            };

            yield return [int32s];
        }

        public static IEnumerable<object[]> TestUInt64s()
        {
            var uint64s = new Dictionary<object, byte[]>
            {
                {0UL, [0x0, 0x2] },
                {500UL, [0x2, 0x2, 0x1, 0xf4] },
                {10872UL, [0x2, 0x2, 0x2a, 0x78] }
            };

            for (var power = 1; power < 8; power++)
            {
                var key = UInt64Pow(2, 8 * power) - 1;
                var value = new byte[2 + power];

                value[0] = (byte)power;
                value[1] = 0x2;
                for (var i = 2; i < value.Length; i++)
                {
                    value[i] = 0xff;
                }

                uint64s.Add(key, value);
            }

            yield return [uint64s];
        }

        public static ulong UInt64Pow(ulong x, int pow)
        {
            ulong ret = 1;
            while (pow != 0)
            {
                if ((pow & 1) == 1)
                    ret *= x;
                x *= x;
                pow >>= 1;
            }
            return ret;
        }

        public static IEnumerable<object[]> TestBigIntegers()
        {
            var bigInts = new Dictionary<object, byte[]>
            {
                {new BigInteger(0), [0x0, 0x3] },
                {new BigInteger(500), [0x2, 0x3, 0x1, 0xf4] },
                {new BigInteger(10872), [0x2, 0x3, 0x2a, 0x78] }
            };

            for (var power = 1; power <= 16; power++)
            {
                var key = BigInteger.Pow(new BigInteger(2), 8 * power) - 1;
                var value = new byte[2 + power];

                value[0] = (byte)power;
                value[1] = 0x3;
                for (var i = 2; i < value.Length; i++)
                {
                    value[i] = 0xff;
                }

                bigInts.Add(key, value);
            }

            yield return [bigInts];
        }

        public static IEnumerable<object[]> TestDoubles()
        {
            var doubles = new Dictionary<object, byte[]>
            {
                {0.0, [0x68, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0] },
                {0.5, [0x68, 0x3F, 0xE0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0] },
                {3.14159265359, [0x68, 0x40, 0x9, 0x21, 0xFB, 0x54, 0x44, 0x2E, 0xEA] },
                {123.0, [0x68, 0x40, 0x5E, 0xC0, 0x0, 0x0, 0x0, 0x0, 0x0] },
                {1073741824.12457, [0x68, 0x41, 0xD0, 0x0, 0x0, 0x0, 0x7, 0xF8, 0xF4] },
                {-0.5, [0x68, 0xBF, 0xE0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0] },
                {-3.14159265359, [0x68, 0xC0, 0x9, 0x21, 0xFB, 0x54, 0x44, 0x2E, 0xEA] },
                {-1073741824.12457, [0x68, 0xC1, 0xD0, 0x0, 0x0, 0x0, 0x7, 0xF8, 0xF4] }
            };

            yield return [doubles];
        }

        public static IEnumerable<object[]> TestFloats()
        {
            var floats = new Dictionary<object, byte[]>
            {
                {(float) 0.0, [0x4, 0x8, 0x0, 0x0, 0x0, 0x0] },
                {(float) 1.0, [0x4, 0x8, 0x3F, 0x80, 0x0, 0x0] },
                {(float) 1.1, [0x4, 0x8, 0x3F, 0x8C, 0xCC, 0xCD] },
                {(float) 3.14, [0x4, 0x8, 0x40, 0x48, 0xF5, 0xC3] },
                {(float) 9999.99, [0x4, 0x8, 0x46, 0x1C, 0x3F, 0xF6] },
                {(float) -1.0, [0x4, 0x8, 0xBF, 0x80, 0x0, 0x0] },
                {(float) -1.1, [0x4, 0x8, 0xBF, 0x8C, 0xCC, 0xCD] },
                {(float) -3.14, [0x4, 0x8, 0xC0, 0x48, 0xF5, 0xC3] },
                {(float) -9999.99, [0x4, 0x8, 0xC6, 0x1C, 0x3F, 0xF6] }
            };

            yield return [floats];
        }

        public static IEnumerable<object[]> TestPointers()
        {
            var pointers = new Dictionary<object, byte[]>
            {
                {0L, [0x20, 0x0] },
                {5L, [0x20, 0x5] },
                {10L, [0x20, 0xa] },
                {(1L << 10) - 1, [0x23, 0xff] },
                {3017L, [0x28, 0x3, 0xc9] },
                {(1L << 19) - 5, [0x2f, 0xf7, 0xfb] },
                {(1L << 19) + (1 << 11) - 1, [0x2f, 0xff, 0xff] },
                {(1L << 27) - 2, [0x37, 0xf7, 0xf7, 0xfe] },
                {(1L << 27) + (1 << 19) + (1 << 11) - 1, [0x37, 0xff, 0xff, 0xff] },
                {(1L << 31) - 1, [0x38, 0x7f, 0xff, 0xff, 0xff] }
            };

            yield return [pointers];
        }

        public static IEnumerable<object[]> TestStrings()
        {
            yield return [Strings()];
        }

        private static Dictionary<string, byte[]> Strings()
        {
            var strings = new Dictionary<string, byte[]>();

            AddTestString(strings, 0x40, "");
            AddTestString(strings, 0x41, "1");
            AddTestString(strings, 0x43, "人");
            AddTestString(strings, 0x43, "123");
            AddTestString(strings, 0x5b, "123456789012345678901234567");
            AddTestString(strings, 0x5c, "1234567890123456789012345678");
            AddTestString(strings, [0x5d, 0x0], "12345678901234567890123456789");
            AddTestString(strings, [0x5d, 0x1], "123456789012345678901234567890");

            AddTestString(strings, [0x5e, 0x0, 0xd7], new string('x', 500));
            AddTestString(strings, [0x5e, 0x6, 0xb3], new string('x', 2000));
            AddTestString(strings, [0x5f, 0x0, 0x10, 0x53], new string('x', 70000));
            return strings;
        }

        private static void AddTestString(Dictionary<string, byte[]> tests, byte ctrl, string str)
        {
            AddTestString(tests, [ctrl], str);
        }

        private static void AddTestString(Dictionary<string, byte[]> tests, byte[] ctrl, string str)
        {
            var sb = Encoding.UTF8.GetBytes(str);
            var bytes = new byte[ctrl.Length + sb.Length];

            Array.Copy(ctrl, 0, bytes, 0, ctrl.Length);
            Array.Copy(sb, 0, bytes, ctrl.Length, sb.Length);
            tests.Add(str, bytes);
        }

        public static IEnumerable<object[]> TestBooleans()
        {
            var booleans = new Dictionary<object, byte[]>
            {
                {false, [0x0, 0x7] },
                {true, [0x1, 0x7] }
            };

            yield return [booleans];
        }

        public static IEnumerable<object[]> TestBytes()
        {
            var bytes = new Dictionary<byte[], byte[]>();

            var strings = Strings();

            foreach (var s in strings.Keys)
            {
                var ba = strings[s];
                ba[0] ^= 0xc0;

                bytes.Add(Encoding.UTF8.GetBytes(s), ba);
            }

            yield return [bytes];
        }

        public static IEnumerable<object[]> TestMaps()
        {
            var maps = new Dictionary<Dictionary<string, object>, byte[]>();

            var empty = new Dictionary<string, object>();
            maps.Add(new Dictionary<string, object>(empty), [0xe0]);

            var one = new Dictionary<string, object> { { "en", "Foo" } };
            maps.Add(new Dictionary<string, object>(one), [
                0xe1, /* en */0x42, 0x65, 0x6e,
                /* Foo */0x43, 0x46, 0x6f, 0x6f
            ]);

            var two = new Dictionary<string, object> { { "en", "Foo" }, { "zh", "人" } };
            maps.Add(new Dictionary<string, object>(two), [
                0xe2,
                /* en */
                0x42, 0x65, 0x6e,
                /* Foo */
                0x43, 0x46, 0x6f, 0x6f,
                /* zh */
                0x42, 0x7a, 0x68,
                /* 人 */
                0x43, 0xe4, 0xba, 0xba
            ]);

            var nested = new Dictionary<string, object> { { "name", two } };

            maps.Add(new Dictionary<string, object>(nested), [
                0xe1, /* name */
                0x44, 0x6e, 0x61, 0x6d, 0x65, 0xe2, /* en */
                0x42, 0x65, 0x6e,
                /* Foo */
                0x43, 0x46, 0x6f, 0x6f,
                /* zh */
                0x42, 0x7a, 0x68,
                /* 人 */
                0x43, 0xe4, 0xba, 0xba
            ]);

            var guess = new Dictionary<string, object>();
            var languages = new List<object> { "en", "zh" };
            guess.Add("languages", languages.AsReadOnly());
            maps.Add(new Dictionary<string, object>(guess), [
                0xe1, /* languages */
                0x49, 0x6c, 0x61, 0x6e, 0x67, 0x75, 0x61, 0x67, 0x65, 0x73,
                /* array */
                0x2, 0x4,
                /* en */
                0x42, 0x65, 0x6e,
                /* zh */
                0x42, 0x7a, 0x68
            ]);

            yield return [maps];
        }

        public static IEnumerable<object[]> TestArrays()
        {
            var arrays = new Dictionary<List<object>, byte[]>();

            var f1 = new List<object> { "Foo" };
            arrays.Add(f1, [
                0x1, 0x4,
                /* Foo */
                0x43, 0x46, 0x6f, 0x6f
            ]);

            var f2 = new List<object> { "Foo", "人" };
            arrays.Add(f2, [
                0x2, 0x4,
                /* Foo */
                0x43, 0x46, 0x6f, 0x6f,
                /* 人 */
                0x43, 0xe4, 0xba, 0xba
            ]);

            var empty = new List<object>();
            arrays.Add(empty, [0x0, 0x4]);

            yield return [arrays];
        }
    }
}
