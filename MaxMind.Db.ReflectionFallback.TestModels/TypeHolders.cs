using System.Collections.Generic;
using System.Numerics;

namespace MaxMind.Db.ReflectionFallback.TestModels
{
    // Mirrors MaxMind.Db.Test.Helper.TypeHolder and PropTypeHolder. Those models live
    // in an assembly that references the source generator, so they exercise generated
    // activation; these are the same shapes on the reflection fallback, which is what
    // every consumer gets until they rebuild against a generator-bearing MaxMind.Db.
    public class ReflectionInnerMapX
    {
        [Constructor]
        public ReflectionInnerMapX(
            string utf8_stringX,
            [Network] Network network,
            LinkedList<long> arrayX
            )
        {
            ArrayX = arrayX;
            Network = network;
            Utf8StringX = utf8_stringX;
        }

        public LinkedList<long> ArrayX { get; }
        public Network Network { get; }
        public string Utf8StringX { get; }
    }

    public class ReflectionInnerMap
    {
        [Constructor]
        public ReflectionInnerMap(ReflectionInnerMapX mapX)
        {
            MapX = mapX;
        }

        public ReflectionInnerMapX MapX { get; }
    }

    public class ReflectionInnerNonexistant
    {
        [Constructor]
        public ReflectionInnerNonexistant(
            [Inject("injected")] string injected,
            [Network] Network network
            )
        {
            Injected = injected;
            Network = network;
        }

        public string Injected { get; }
        public Network Network { get; }
    }

    public class ReflectionNonexistant
    {
        [Constructor]
        public ReflectionNonexistant(
            [MapKey("innerNonexistant", true)] ReflectionInnerNonexistant innerNonexistant,
            [Inject("injected")] string injected,
            [Network] Network network
            )
        {
            Injected = injected;
            InnerNonexistant = innerNonexistant;
            Network = network;
        }

        public string Injected { get; }
        public ReflectionInnerNonexistant InnerNonexistant { get; }
        public Network Network { get; }
    }

    public class ReflectionTypeHolder
    {
        [Constructor]
        public ReflectionTypeHolder(
            string utf8_string,
            byte[] bytes,
            int uint16,
            long uint32,
            ulong uint64,
            BigInteger uint128,
            int int32,
            bool boolean,
            ICollection<long> array,
            [MapKey("double")] double mmDouble,
            [MapKey("float")] float mmFloat,
            [MapKey("map")] ReflectionInnerMap map,
            [MapKey("nonexistant", true)] ReflectionNonexistant nonexistant
            )
        {
            Array = array;
            Boolean = boolean;
            Bytes = bytes;
            Double = mmDouble;
            Float = mmFloat;
            Int32 = int32;
            Map = map;
            Nonexistant = nonexistant;
            Uint16 = uint16;
            Uint32 = uint32;
            Uint64 = uint64;
            Uint128 = uint128;
            Utf8String = utf8_string;
        }

        public ICollection<long> Array { get; }
        public bool Boolean { get; }
        public byte[] Bytes { get; }
        public double Double { get; }
        public float Float { get; }
        public long Int32 { get; }
        public ReflectionInnerMap Map { get; }
        public ReflectionNonexistant Nonexistant { get; }
        public int Uint16 { get; }
        public long Uint32 { get; }
        public ulong Uint64 { get; }
        public BigInteger Uint128 { get; }
        public string Utf8String { get; }
    }

    public class ReflectionPropInnerMapX
    {
        [MapKey("arrayX")]
        public LinkedList<long>? ArrayX { get; set; }

        [Network]
        public Network? Network { get; set; }

        [MapKey("utf8_stringX")]
        public string? Utf8StringX { get; set; }
    }

    public class ReflectionPropInnerMap
    {
        [MapKey("mapX")]
        public ReflectionPropInnerMapX? MapX { get; set; }
    }

    public class ReflectionPropTypeHolder
    {
        [MapKey("array")]
        public ICollection<long>? Array { get; set; }

        [MapKey("boolean")]
        public bool Boolean { get; set; }

        [MapKey("bytes")]
        public byte[]? Bytes { get; set; }

        [MapKey("double")]
        public double Double { get; set; }

        [MapKey("float")]
        public float Float { get; set; }

        [MapKey("int32")]
        public int Int32 { get; set; }

        [MapKey("map")]
        public ReflectionPropInnerMap? Map { get; set; }

        [Inject("injected")]
        public string? Injected { get; set; }

        [Network]
        public Network? Network { get; set; }

        [MapKey("uint128")]
        public BigInteger Uint128 { get; set; }

        [MapKey("uint16")]
        public int Uint16 { get; set; }

        [MapKey("uint32")]
        public long Uint32 { get; set; }

        [MapKey("uint64")]
        public ulong Uint64 { get; set; }

        [MapKey("utf8_string")]
        public string? Utf8String { get; set; }
    }
}
