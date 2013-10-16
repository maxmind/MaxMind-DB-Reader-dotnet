using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MaxMind.MaxMindDb.Test
{
    [TestFixture]
    public class DecoderTest
    {
        [Test]
        public void TestUInt16()
        {
            var uint16s = new Dictionary<int, byte[]>();

            uint16s.Add(0, new byte[] { (byte) 0xa0 });
            uint16s.Add((1 << 8) - 1, new byte[] { (byte) 0xa1, (byte) 0xff });
            uint16s.Add(500, new byte[] { (byte) 0xa2, 0x1, (byte) 0xf4 });
            uint16s.Add(10872, new byte[] { (byte) 0xa2, 0x2a, 0x78 });
            uint16s.Add(UInt16.MaxValue, new byte[] { (byte) 0xa2, (byte) 0xff, (byte) 0xff });

            TestTypeDecoding(uint16s);
        }

        [Test]
        public void TestUInt32()
        {
            var uint32s = new Dictionary<long, byte[]>();

            uint32s.Add((long) 0, new byte[] { (byte) 0xc0 });
            uint32s.Add((long) ((1 << 8) - 1), new byte[] { (byte) 0xc1, (byte) 0xff });
            uint32s.Add((long) 500, new byte[] { (byte) 0xc2, 0x1, (byte) 0xf4 });
            uint32s.Add((long) 10872, new byte[] { (byte) 0xc2, 0x2a, 0x78 });
            uint32s.Add((long) ((1 << 16) - 1), new byte[] { (byte) 0xc2, (byte) 0xff, (byte) 0xff });
            uint32s.Add((long) ((1 << 24) - 1), new byte[] { (byte) 0xc3, (byte) 0xff, (byte) 0xff, (byte) 0xff });
            uint32s.Add(UInt32.MaxValue, new byte[] { (byte) 0xc4, (byte) 0xff, (byte) 0xff, (byte) 0xff, (byte) 0xff });

            TestTypeDecoding(uint32s);
        }

        [Test]
        public void TestInt32s()
        {
            var int32s = new Dictionary<int, byte[]>();

            int32s.Add(0, new byte[] { 0x0, 0x1 });
            int32s.Add(-1, new byte[] { 0x4, 0x1, (byte) 0xff, (byte) 0xff, (byte) 0xff, (byte) 0xff });
            int32s.Add((2 << 7) - 1, new byte[] { 0x1, 0x1, (byte) 0xff });
            int32s.Add(1 - (2 << 7), new byte[] { 0x4, 0x1, (byte) 0xff, (byte) 0xff, (byte) 0xff, 0x1 });
            int32s.Add(500, new byte[] { 0x2, 0x1, 0x1, (byte) 0xf4 });
            int32s.Add(-500, new byte[] { 0x4, 0x1, (byte) 0xff, (byte) 0xff, (byte) 0xfe, 0xc });
            int32s.Add((2 << 15) - 1, new byte[] { 0x2, 0x1, (byte) 0xff, (byte) 0xff });
            int32s.Add(1 - (2 << 15), new byte[] { 0x4, 0x1, (byte) 0xff, (byte) 0xff, 0x0, 0x1 });
            int32s.Add((2 << 23) - 1, new byte[] { 0x3, 0x1, (byte) 0xff, (byte) 0xff, (byte) 0xff });
            int32s.Add(1 - (2 << 23), new byte[] { 0x4, 0x1, (byte) 0xff, 0x0, 0x0, 0x1 });
            int32s.Add(int.MaxValue, new byte[] { 0x4, 0x1, 0x7f, (byte) 0xff, (byte) 0xff, (byte) 0xff });
            int32s.Add(-int.MaxValue, new byte[] { 0x4, 0x1, (byte) 0x80, 0x0, 0x0, 0x1 });

            TestTypeDecoding(int32s);
        }

        [Test]
        public void TestInt64s()
        {
            var int64s = new Dictionary<Int64, byte[]>();

            int64s.Add(0L, new byte[]{0x0, 0x2});
            int64s.Add(500L, new byte[]{0x2, 0x2, 0x1, 0xf4});
            int64s.Add(10872, new byte[]{0x2, 0x2, 0x2a, 0x78});

            for (int power = 1; power < 8; power++)
            {
                var key = Int64Pow(2, 8*power)-1;
                var value = new byte[2 + power];

                value[0] = (byte) power;
                value[1] = 0x2;
                for (int i = 2; i < value.Length; i++) 
                {
                    value[i] = (byte) 0xff;
                }

                int64s.Add(key, value);
            }

            TestTypeDecoding(int64s);
        }

        static long Int64Pow(long x, int pow)
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
            var bigInts = new Dictionary<BigInteger, byte[]>();
            bigInts.Add(new BigInteger(0), new byte[]{0x0,0x3});
            bigInts.Add(new BigInteger(500), new byte[]{0x2,0x3, 0x1, 0xf4});
            bigInts.Add(new BigInteger(10872), new byte[]{0x2,0x3, 0x2a, 0x78});

            for (int power = 1; power <= 16; power++)
            {
                var key = BigIntegerPow(new BigInteger(2), 8*power)-1;
                var value = new byte[2 + power];

                value[0] = (byte) power;
                value[1] = 0x3;
                for (int i = 2; i < value.Length; i++) 
                {
                    value[i] = (byte) 0xff;
                }

                bigInts.Add(key, value);
            }

            TestTypeDecoding(bigInts);
        }

        static BigInteger BigIntegerPow(BigInteger x, int pow)
        {
            var ret = new BigInteger(1);
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
        public void TestDoubles()
        {
            var doubles = new Dictionary<double, byte[]>();
            doubles.Add(0.0, new byte[] { 0x68, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 });
            doubles.Add(0.5, new byte[] { 0x68, 0x3F, (byte) 0xE0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 });
            doubles.Add(3.14159265359, new byte[] { 0x68, 0x40, 0x9, 0x21, (byte) 0xFB, 0x54, 0x44, 0x2E, (byte) 0xEA });
            doubles.Add(123.0, new byte[] { 0x68, 0x40, 0x5E, (byte) 0xC0, 0x0, 0x0, 0x0, 0x0, 0x0 });
            doubles.Add(1073741824.12457, new byte[] { 0x68, 0x41, (byte) 0xD0, 0x0, 0x0, 0x0, 0x7, (byte) 0xF8, (byte) 0xF4 });
            doubles.Add(-0.5, new byte[] { 0x68, (byte) 0xBF, (byte) 0xE0, 0x0, 0x0, 0x0, 0x0, 0x0, 0x0 });
            doubles.Add(-3.14159265359, new byte[] { 0x68, (byte) 0xC0, 0x9, 0x21, (byte) 0xFB, 0x54, 0x44, 0x2E, (byte) 0xEA });
            doubles.Add(-1073741824.12457, new byte[] { 0x68, (byte) 0xC1, (byte) 0xD0, 0x0, 0x0, 0x0, 0x7, (byte) 0xF8, (byte) 0xF4 });

            TestTypeDecoding(doubles);
        }

        [Test]
        public void TestFloats()
        {
            var floats = new Dictionary<float, byte[]>();
            floats.Add((float) 0.0, new byte[] { 0x4, 0x8, 0x0, 0x0, 0x0, 0x0 });
            floats.Add((float) 1.0, new byte[] { 0x4, 0x8, 0x3F, (byte) 0x80, 0x0, 0x0 });
            floats.Add((float) 1.1, new byte[] { 0x4, 0x8, 0x3F, (byte) 0x8C, (byte) 0xCC, (byte) 0xCD });
            floats.Add((float) 3.14, new byte[] { 0x4, 0x8, 0x40, 0x48, (byte) 0xF5, (byte) 0xC3 });
            floats.Add((float) 9999.99, new byte[] { 0x4, 0x8, 0x46, 0x1C, 0x3F, (byte) 0xF6 });
            floats.Add((float) -1.0, new byte[] { 0x4, 0x8, (byte) 0xBF, (byte) 0x80, 0x0, 0x0 });
            floats.Add((float) -1.1, new byte[] { 0x4, 0x8, (byte) 0xBF, (byte) 0x8C, (byte) 0xCC, (byte) 0xCD });
            floats.Add((float) -3.14, new byte[] { 0x4, 0x8, (byte) 0xC0, 0x48, (byte) 0xF5, (byte) 0xC3 });
            floats.Add((float) -9999.99, new byte[] { 0x4, 0x8, (byte) 0xC6, 0x1C, 0x3F, (byte) 0xF6 });

            TestTypeDecoding(floats);
        }

        [Test]
        public void TestPointers()
        {
            var pointers = new Dictionary<long, byte[]>();

            pointers.Add((long) 0, new byte[] { 0x20, 0x0 });
            pointers.Add((long) 5, new byte[] { 0x20, 0x5 });
            pointers.Add((long) 10, new byte[] { 0x20, 0xa });
            pointers.Add((long) ((1 << 10) - 1), new byte[] { 0x23, (byte) 0xff, });
            pointers.Add((long) 3017, new byte[] { 0x28, 0x3, (byte) 0xc9 });
            pointers.Add((long) ((1 << 19) - 5), new byte[] { 0x2f, (byte) 0xf7, (byte) 0xfb });
            pointers.Add((long) ((1 << 19) + (1 << 11) - 1), new byte[] { 0x2f, (byte) 0xff, (byte) 0xff });
            pointers.Add((long) ((1 << 27) - 2), new byte[] { 0x37, (byte) 0xf7, (byte) 0xf7, (byte) 0xfe });
            pointers.Add((((long) 1) << 27) + (1 << 19) + (1 << 11) - 1, new byte[] { 0x37, (byte) 0xff, (byte) 0xff, (byte) 0xff });
            pointers.Add((((long) 1) << 31) - 1, new byte[] { 0x38, (byte) 0x7f, (byte) 0xff, (byte) 0xff, (byte) 0xff });

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

            DecoderTest.AddTestString(strings, (byte) 0x40, "");
            DecoderTest.AddTestString(strings, (byte) 0x41, "1");
            DecoderTest.AddTestString(strings, (byte) 0x43, "人");
            DecoderTest.AddTestString(strings, (byte) 0x43, "123");
            DecoderTest.AddTestString(strings, (byte) 0x5b, "123456789012345678901234567");
            DecoderTest.AddTestString(strings, (byte) 0x5c, "1234567890123456789012345678");
            DecoderTest.AddTestString(strings, new byte[] {0x5d, 0x0}, "12345678901234567890123456789");
            DecoderTest.AddTestString(strings, new byte[] {0x5d, 0x1}, "123456789012345678901234567890");

            DecoderTest.AddTestString(strings, new byte[] {0x5e, 0x0, (byte) 0xd7}, new string('x', 500));
            DecoderTest.AddTestString(strings, new byte[] {0x5e, 0x6, (byte) 0xb3}, new string('x', 2000));
            DecoderTest.AddTestString(strings, new byte[] {0x5f, 0x0, 0x10, 0x53,}, new string('x', 70000));
            return strings;
        }

        private static void AddTestString(Dictionary<string, byte[]> tests, byte ctrl, string str) 
        {
            DecoderTest.AddTestString(tests, new byte[] { ctrl }, str);
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
            var booleans = new Dictionary<bool, byte[]>();

            booleans.Add(false, new byte[] { 0x0, 0x7 });
            booleans.Add(true, new byte[] { 0x1, 0x7 });

            TestTypeDecoding(booleans);
        }

        [Test]
        public void TestBytes()
        {
            var bytes = new Dictionary<byte[], byte[]>();

            var strings = DecoderTest.Strings();

            foreach (string s in strings.Keys) 
            {
                byte[] ba = strings[s];
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
            maps.Add(empty, new byte[] { (byte) 0xe0 });

            var one = new JObject();
            one.Add("en", "Foo");
            maps.Add(one, new byte[] { (byte) 0xe1, /* en */0x42, 0x65, 0x6e,
                /* Foo */0x43, 0x46, 0x6f, 0x6f });

            var two = new JObject();
            two.Add("en", "Foo");
            two.Add("zh", "人");
            maps.Add(two, new byte[] { (byte) 0xe2,
            /* en */
            0x42, 0x65, 0x6e,
            /* Foo */
            0x43, 0x46, 0x6f, 0x6f,
            /* zh */
            0x42, 0x7a, 0x68,
            /* 人 */
            0x43, (byte) 0xe4, (byte) 0xba, (byte) 0xba });

            var nested = new JObject();
            nested.Add("name", two);

            maps.Add(nested, new byte[] { (byte) 0xe1, /* name */
            0x44, 0x6e, 0x61, 0x6d, 0x65, (byte) 0xe2,/* en */
            0x42, 0x65, 0x6e,
            /* Foo */
            0x43, 0x46, 0x6f, 0x6f,
            /* zh */
            0x42, 0x7a, 0x68,
            /* 人 */
            0x43, (byte) 0xe4, (byte) 0xba, (byte) 0xba });

            var guess = new JObject();
            var languages = new JArray();
            languages.Add("en");
            languages.Add("zh");
            guess.Add("languages", languages);
            maps.Add(guess, new byte[] { (byte) 0xe1,/* languages */
            0x49, 0x6c, 0x61, 0x6e, 0x67, 0x75, 0x61, 0x67, 0x65, 0x73,
            /* array */
            0x2, 0x4,
            /* en */
            0x42, 0x65, 0x6e,
            /* zh */
            0x42, 0x7a, 0x68 });

            TestTypeDecoding(maps);
        }

        [Test]
        public void TestArrays()
        {
            var arrays = new Dictionary<JArray, byte[]>();

            var f1 = new JArray();
            f1.Add("Foo");
            arrays.Add(f1, new byte[] { 0x1, 0x4,
            /* Foo */
            0x43, 0x46, 0x6f, 0x6f });

            var f2 = new JArray();
            f2.Add("Foo");
            f2.Add("人");
            arrays.Add(f2, new byte[] { 0x2, 0x4,
            /* Foo */
            0x43, 0x46, 0x6f, 0x6f,
            /* 人 */
            0x43, (byte) 0xe4, (byte) 0xba, (byte) 0xba });

            var empty = new JArray();
            arrays.Add(empty, new byte[] { 0x0, 0x4 });

            TestTypeDecoding(arrays);
        }

        private static void TestTypeDecoding<T>(Dictionary<T, byte[]> tests)
        {
            foreach (KeyValuePair<T, byte[]> entry in tests)
            {
                var expect = entry.Key;
                var input = entry.Value;

                using (var stream = new MemoryStream(input))
                {
                    var decoder = new Decoder(stream, 0);
                    decoder.pointerTestHack = true;
                    var jToken = decoder.Decode(0).Node;

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