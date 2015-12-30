#region

using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

#endregion

namespace MaxMind.Db.Test
{
    [TestFixture]
    public class DecoderTest
    {
        [Test]
        public void TestUInt16()
        {
            var uint16s = new Dictionary<int, byte[]>
            {
                {0, new byte[] {0xa0}},
                {(1 << 8) - 1, new[] {(byte) 0xa1, (byte) 0xff}},
                {500, new byte[] {0xa2, 0x1, 0xf4}},
                {10872, new byte[] {0xa2, 0x2a, 0x78}},
                {ushort.MaxValue, new[] {(byte) 0xa2, (byte) 0xff, (byte) 0xff}}
            };

            TestTypeDecoding(uint16s);
        }

        [Test]
        public void TestUInt32()
        {
            var uint32s = new Dictionary<long, byte[]>
            {
                {0, new[] {(byte) 0xc0}},
                {(1 << 8) - 1, new[] {(byte) 0xc1, (byte) 0xff}},
                {500, new byte[] {0xc2, 0x1, 0xf4}},
                {10872, new byte[] {0xc2, 0x2a, 0x78}},
                {(1 << 16) - 1, new[] {(byte) 0xc2, (byte) 0xff, (byte) 0xff}},
                {(1 << 24) - 1, new[] {(byte) 0xc3, (byte) 0xff, (byte) 0xff, (byte) 0xff}},
                {uint.MaxValue, new[] {(byte) 0xc4, (byte) 0xff, (byte) 0xff, (byte) 0xff, (byte) 0xff}}
            };

            TestTypeDecoding(uint32s);
        }

        [Test]
        public void TestInt32s()
        {
            var int32s = new Dictionary<int, byte[]>
            {
                {0, new byte[] {0x0, 0x1}},
                {-1, new byte[] {0x4, 0x1, 0xff, 0xff, 0xff, 0xff}},
                {(2 << 7) - 1, new byte[] {0x1, 0x1, 0xff}},
                {1 - (2 << 7), new byte[] {0x4, 0x1, 0xff, 0xff, 0xff, 0x1}},
                {500, new byte[] {0x2, 0x1, 0x1, 0xf4}},
                {-500, new byte[] {0x4, 0x1, 0xff, 0xff, 0xfe, 0xc}},
                {(2 << 15) - 1, new byte[] {0x2, 0x1, 0xff, 0xff}},
                {1 - (2 << 15), new byte[] {0x4, 0x1, 0xff, 0xff, 0x0, 0x1}},
                {(2 << 23) - 1, new byte[] {0x3, 0x1, 0xff, 0xff, 0xff}},
                {1 - (2 << 23), new byte[] {0x4, 0x1, 0xff, 0x0, 0x0, 0x1}},
                {int.MaxValue, new byte[] {0x4, 0x1, 0x7f, 0xff, 0xff, 0xff}},
                {-int.MaxValue, new byte[] {0x4, 0x1, 0x80, 0x0, 0x0, 0x1}}
            };

            TestTypeDecoding(int32s);
        }

        [Test]
        public void TestInt64s()
        {
            var int64s = new Dictionary<long, byte[]>
            {
                {0L, new byte[] {0x0, 0x2}},
                {500L, new byte[] {0x2, 0x2, 0x1, 0xf4}},
                {10872, new byte[] {0x2, 0x2, 0x2a, 0x78}}
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

            TestTypeDecoding(int64s);
        }

        private static long Int64Pow(long x, int pow)
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

        [Test]
        public void TestBigIntegers()
        {
            var bigInts = new Dictionary<BigInteger, byte[]>
            {
                {new BigInteger(0), new byte[] {0x0, 0x3}},
                {new BigInteger(500), new byte[] {0x2, 0x3, 0x1, 0xf4}},
                {new BigInteger(10872), new byte[] {0x2, 0x3, 0x2a, 0x78}}
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

            TestTypeDecoding(bigInts);
        }

        [Test]
        public void TestDoubles()
        {
            var doubles = new Dictionary<double, byte[]>
            {
                {0.0, new byte[] {0x68, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0}},
                {0.5, new byte[] {0x68, 0x3F, 0xE0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0}},
                {3.14159265359, new byte[] {0x68, 0x40, 0x9, 0x21, 0xFB, 0x54, 0x44, 0x2E, 0xEA}},
                {123.0, new byte[] {0x68, 0x40, 0x5E, 0xC0, 0x0, 0x0, 0x0, 0x0, 0x0}},
                {1073741824.12457, new byte[] {0x68, 0x41, 0xD0, 0x0, 0x0, 0x0, 0x7, 0xF8, 0xF4}},
                {-0.5, new byte[] {0x68, 0xBF, 0xE0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0}},
                {-3.14159265359, new byte[] {0x68, 0xC0, 0x9, 0x21, 0xFB, 0x54, 0x44, 0x2E, 0xEA}},
                {-1073741824.12457, new byte[] {0x68, 0xC1, 0xD0, 0x0, 0x0, 0x0, 0x7, 0xF8, 0xF4}}
            };

            TestTypeDecoding(doubles);
        }

        [Test]
        public void TestFloats()
        {
            var floats = new Dictionary<float, byte[]>
            {
                {(float) 0.0, new byte[] {0x4, 0x8, 0x0, 0x0, 0x0, 0x0}},
                {(float) 1.0, new byte[] {0x4, 0x8, 0x3F, 0x80, 0x0, 0x0}},
                {(float) 1.1, new byte[] {0x4, 0x8, 0x3F, 0x8C, 0xCC, 0xCD}},
                {(float) 3.14, new byte[] {0x4, 0x8, 0x40, 0x48, 0xF5, 0xC3}},
                {(float) 9999.99, new byte[] {0x4, 0x8, 0x46, 0x1C, 0x3F, 0xF6}},
                {(float) -1.0, new byte[] {0x4, 0x8, 0xBF, 0x80, 0x0, 0x0}},
                {(float) -1.1, new byte[] {0x4, 0x8, 0xBF, 0x8C, 0xCC, 0xCD}},
                {(float) -3.14, new byte[] {0x4, 0x8, 0xC0, 0x48, 0xF5, 0xC3}},
                {(float) -9999.99, new byte[] {0x4, 0x8, 0xC6, 0x1C, 0x3F, 0xF6}}
            };

            TestTypeDecoding(floats);
        }

        [Test]
        public void TestPointers()
        {
            var pointers = new Dictionary<long, byte[]>
            {
                {0, new byte[] {0x20, 0x0}},
                {5, new byte[] {0x20, 0x5}},
                {10, new byte[] {0x20, 0xa}},
                {(1 << 10) - 1, new byte[] {0x23, 0xff}},
                {3017, new byte[] {0x28, 0x3, 0xc9}},
                {(1 << 19) - 5, new byte[] {0x2f, 0xf7, 0xfb}},
                {(1 << 19) + (1 << 11) - 1, new byte[] {0x2f, 0xff, 0xff}},
                {(1 << 27) - 2, new byte[] {0x37, 0xf7, 0xf7, 0xfe}},
                {(((long) 1) << 27) + (1 << 19) + (1 << 11) - 1, new byte[] {0x37, 0xff, 0xff, 0xff}},
                {(((long) 1) << 31) - 1, new byte[] {0x38, 0x7f, 0xff, 0xff, 0xff}}
            };

            TestTypeDecoding(pointers);
        }

        [Test]
        public void TestStrings()
        {
            TestTypeDecoding(Strings());
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
            AddTestString(strings, new byte[] { 0x5d, 0x0 }, "12345678901234567890123456789");
            AddTestString(strings, new byte[] { 0x5d, 0x1 }, "123456789012345678901234567890");

            AddTestString(strings, new byte[] { 0x5e, 0x0, 0xd7 }, new string('x', 500));
            AddTestString(strings, new byte[] { 0x5e, 0x6, 0xb3 }, new string('x', 2000));
            AddTestString(strings, new byte[] { 0x5f, 0x0, 0x10, 0x53 }, new string('x', 70000));
            return strings;
        }

        private static void AddTestString(Dictionary<string, byte[]> tests, byte ctrl, string str)
        {
            AddTestString(tests, new[] { ctrl }, str);
        }

        private static void AddTestString(Dictionary<string, byte[]> tests, byte[] ctrl, string str)
        {
            var sb = Encoding.UTF8.GetBytes(str);
            var bytes = new byte[ctrl.Length + sb.Length];

            Array.Copy(ctrl, 0, bytes, 0, ctrl.Length);
            Array.Copy(sb, 0, bytes, ctrl.Length, sb.Length);
            tests.Add(str, bytes);
        }

        [Test]
        public void TestBooleans()
        {
            var booleans = new Dictionary<bool, byte[]> { { false, new byte[] { 0x0, 0x7 } }, { true, new byte[] { 0x1, 0x7 } } };

            TestTypeDecoding(booleans);
        }

        [Test]
        public void TestBytes()
        {
            var bytes = new Dictionary<byte[], byte[]>();

            var strings = Strings();

            foreach (var s in strings.Keys)
            {
                var ba = strings[s];
                ba[0] ^= 0xc0;

                bytes.Add(Encoding.UTF8.GetBytes(s), ba);
            }

            TestTypeDecoding(bytes);
        }

        [Test]
        public void TestMaps()
        {
            var maps = new Dictionary<JObject, byte[]>();

            var empty = new JObject();
            maps.Add(empty, new[] { (byte)0xe0 });

            var one = new JObject { { "en", "Foo" } };
            maps.Add(one, new byte[]
            {
                0xe1, /* en */0x42, 0x65, 0x6e,
                /* Foo */0x43, 0x46, 0x6f, 0x6f
            });

            var two = new JObject { { "en", "Foo" }, { "zh", "人" } };
            maps.Add(two, new byte[]
            {
                0xe2,
                /* en */
                0x42, 0x65, 0x6e,
                /* Foo */
                0x43, 0x46, 0x6f, 0x6f,
                /* zh */
                0x42, 0x7a, 0x68,
                /* 人 */
                0x43, 0xe4, 0xba, 0xba
            });

            var nested = new JObject { { "name", two } };

            maps.Add(nested, new byte[]
            {
                0xe1, /* name */
                0x44, 0x6e, 0x61, 0x6d, 0x65, 0xe2, /* en */
                0x42, 0x65, 0x6e,
                /* Foo */
                0x43, 0x46, 0x6f, 0x6f,
                /* zh */
                0x42, 0x7a, 0x68,
                /* 人 */
                0x43, 0xe4, 0xba, 0xba
            });

            var guess = new JObject();
            var languages = new JArray { "en", "zh" };
            guess.Add("languages", languages);
            maps.Add(guess, new byte[]
            {
                0xe1, /* languages */
                0x49, 0x6c, 0x61, 0x6e, 0x67, 0x75, 0x61, 0x67, 0x65, 0x73,
                /* array */
                0x2, 0x4,
                /* en */
                0x42, 0x65, 0x6e,
                /* zh */
                0x42, 0x7a, 0x68
            });

            TestTypeDecoding(maps);
        }

        [Test]
        public void TestArrays()
        {
            var arrays = new Dictionary<JArray, byte[]>();

            var f1 = new JArray { "Foo" };
            arrays.Add(f1, new byte[]
            {
                0x1, 0x4,
                /* Foo */
                0x43, 0x46, 0x6f, 0x6f
            });

            var f2 = new JArray { "Foo", "人" };
            arrays.Add(f2, new byte[]
            {
                0x2, 0x4,
                /* Foo */
                0x43, 0x46, 0x6f, 0x6f,
                /* 人 */
                0x43, 0xe4, 0xba, 0xba
            });

            var empty = new JArray();
            arrays.Add(empty, new byte[] { 0x0, 0x4 });

            TestTypeDecoding(arrays);
        }

        private static void TestTypeDecoding<T>(Dictionary<T, byte[]> tests)
        {
            foreach (var entry in tests)
            {
                var expect = entry.Key;
                var input = entry.Value;

                using (var database = new ArrayReader(input))
                {
                    var decoder = new Decoder(database, 0) { PointerTestHack = true };
                    int offset;
                    var jToken = decoder.Decode(0, out offset);

                    if (jToken is JRaw)
                    {
                        var obj = jToken.ToObject<T>();
                        Assert.That(obj, Is.EqualTo(expect));
                    }
                    else
                    {
                        var jValue = jToken as JValue;
                        if (jValue != null)
                            Assert.That(jValue.Value, Is.EqualTo(expect));
                    }
                }
            }
        }
    }
}