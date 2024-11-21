#region

using FluentAssertions;
using System;
using System.Collections.Generic;
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
        [MemberData(nameof(TestInt64s))]
        [MemberData(nameof(TestBigIntegers))]
        [MemberData(nameof(TestDoubles))]
        [MemberData(nameof(TestFloats))]
        [MemberData(nameof(TestPointers))]
        [MemberData(nameof(TestStrings))]
        [MemberData(nameof(TestBooleans))]
        [MemberData(nameof(TestBytes))]
        [MemberData(nameof(TestMaps))]
        [MemberData(nameof(TestArrays))]
        public static void TestTypeDecoding<T>(Dictionary<T, byte[]> tests, bool useShouldBe = false) where T : class
        {
            foreach (var entry in tests)
            {
                var expect = entry.Key;
                var input = entry.Value;

                using var database = new ArrayBuffer(input);
                var decoder = new Decoder(database, 0, false);
                var val = decoder.Decode<T>(0, out _);
                if (useShouldBe)
                {
                    val.Should().Be(expect);
                }
                else
                {
                    val.Should().BeEquivalentTo(expect, options => options.RespectingRuntimeTypes());
                }
            }
        }

        public static IEnumerable<object[]> TestUInt16()
        {
            var uint16s = new Dictionary<object, byte[]>
            {
                {0, [0xa0] },
                {(1 << 8) - 1, [(byte) 0xa1, (byte) 0xff] },
                {500, [0xa2, 0x1, 0xf4] },
                {10872, [0xa2, 0x2a, 0x78] },
                {ushort.MaxValue, [(byte) 0xa2, (byte) 0xff, (byte) 0xff] }
            };

            yield return [uint16s];
        }

        public static IEnumerable<object[]> TestUInt32()
        {
            var uint32s = new Dictionary<object, byte[]>
            {
                {0, [(byte) 0xc0] },
                {(1 << 8) - 1, [(byte) 0xc1, (byte) 0xff] },
                {500, [0xc2, 0x1, 0xf4] },
                {10872, [0xc2, 0x2a, 0x78] },
                {(1 << 16) - 1, [(byte) 0xc2, (byte) 0xff, (byte) 0xff] },
                {(1 << 24) - 1, [(byte) 0xc3, (byte) 0xff, (byte) 0xff, (byte) 0xff] },
                {uint.MaxValue, [(byte) 0xc4, (byte) 0xff, (byte) 0xff, (byte) 0xff, (byte) 0xff] }
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

        public static IEnumerable<object[]> TestInt64s()
        {
            var int64s = new Dictionary<object, byte[]>
            {
                {0L, [0x0, 0x2] },
                {500L, [0x2, 0x2, 0x1, 0xf4] },
                {10872, [0x2, 0x2, 0x2a, 0x78] }
            };

            for (var power = 1; power < 8; power++)
            {
                var key = Int64Pow(2, 8 * power) - 1;
                var value = new byte[2 + power];

                value[0] = (byte)power;
                value[1] = 0x2;
                for (var i = 2; i < value.Length; i++)
                {
                    value[i] = 0xff;
                }

                int64s.Add(key, value);
            }

            yield return [int64s];
        }

        public static long Int64Pow(long x, int pow)
        {
            long ret = 1;
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

            yield return [bigInts, /*useShouldBe*/ true];
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
                {0, [0x20, 0x0] },
                {5, [0x20, 0x5] },
                {10, [0x20, 0xa] },
                {(1 << 10) - 1, [0x23, 0xff] },
                {3017, [0x28, 0x3, 0xc9] },
                {(1 << 19) - 5, [0x2f, 0xf7, 0xfb] },
                {(1 << 19) + (1 << 11) - 1, [0x2f, 0xff, 0xff] },
                {(1 << 27) - 2, [0x37, 0xf7, 0xf7, 0xfe] },
                {((long) 1 << 27) + (1 << 19) + (1 << 11) - 1, [0x37, 0xff, 0xff, 0xff] },
                {((long) 1 << 31) - 1, [0x38, 0x7f, 0xff, 0xff, 0xff] }
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
            maps.Add(new Dictionary<string, object>(empty), [(byte)0xe0]);

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
