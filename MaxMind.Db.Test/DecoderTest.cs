#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
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

        [Theory]
        [InlineData(32_768, false)]
        [InlineData(32_769, true)]
        public static void TestFlatScalarPointerTargetsConsumeValueBudget(int pointerCount, bool exceedsLimit)
        {
            // The array charges each pointer field, and following each pointer
            // charges its scalar target. At 32,768 pointers the two charges use
            // the full 65,536-value budget. One more must be rejected. This is
            // intentionally flat so neither depth nor exponential fan-out can
            // hide incorrect target accounting.
            var encodedSize = pointerCount - 285;
            var bytes = new List<byte>(pointerCount * 2 + 5)
            {
                0x40, // target: empty UTF-8 string
                0x1E, 0x04, // array with a two-byte encoded size
                (byte)(encodedSize >> 8), (byte)encodedSize,
            };
            for (var i = 0; i < pointerCount; i++)
            {
                WritePointer1(bytes, 0);
            }

            using var database = new MemoryMapBuffer(new MemoryStream(bytes.ToArray(), writable: false));
            var decoder = new Decoder(database, 0);
            if (exceedsLimit)
            {
                var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(1, out _));
                Assert.Equal(
                    "The MaxMind DB file's data section exceeds the maximum number of values.",
                    ex.Message);
            }
            else
            {
                var decoded = Assert.IsType<List<object>>(decoder.Decode<object>(1, out var offset));
                Assert.Equal(pointerCount, decoded.Count);
                Assert.Equal(bytes.Count, offset);
            }
        }

        [Theory]
        [InlineData(21_845, false)]
        [InlineData(21_846, true)]
        public static void TestFlatModelKeyPointerTargetsConsumeValueBudget(int pointerCount, bool exceedsLimit)
        {
            // A map charges its key and value fields, and following each key
            // pointer charges the UTF-8 target that DecodeKey hashes. At 21,845
            // entries those charges use 65,535 values. One more entry crosses
            // the limit. Empty keys are unknown to KeyOnlyModel, so their false
            // values are skipped without introducing another pointer path.
            var encodedSize = pointerCount - 285;
            var bytes = new List<byte>(pointerCount * 4 + 4)
            {
                0x40, // target: empty UTF-8 string
                0xFE, // map with a two-byte encoded size
                (byte)(encodedSize >> 8), (byte)encodedSize,
            };
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
                var ex = Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<KeyOnlyModel>(1, out _));
                Assert.Equal(
                    "The MaxMind DB file's data section exceeds the maximum number of values.",
                    ex.Message);
            }
            else
            {
                var decoded = decoder.Decode<KeyOnlyModel>(1, out var offset);
                Assert.Null(decoded.Name);
                Assert.Equal(bytes.Count, offset);
            }
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
        public static void TestCyclicPointerThrows()
        {
            // A pointer to itself must throw a catchable InvalidDatabaseException
            // rather than recursing until the stack overflows.
            using var database = new MemoryMapBuffer(new MemoryStream([0x20, 0x00], writable: false));
            var decoder = new Decoder(database, 0);
            Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<object>(0, out _));
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
            // The unknown map value begins at depth one. Its 513th nested
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
            // 0xe1: map with one entry. The key at offset 1 is a one-byte
            // pointer (0x20 0x01) whose target is offset 1, the pointer itself.
            using var database = new MemoryMapBuffer(new MemoryStream([0xe1, 0x20, 0x01], writable: false));
            var decoder = new Decoder(database, 0);
            Assert.Throws<InvalidDatabaseException>(() => decoder.Decode<KeyOnlyModel>(0, out _));
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
