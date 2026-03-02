#region

using System.Collections.Generic;
using System.Numerics;

#endregion

namespace MaxMind.Db.Test.Helper
{
    // Property-based equivalents of the constructor-based test models.
    // These use [MapKey]/[Inject]/[Network] on properties instead of
    // constructor parameters, exercising the property-based activation path.

    public class PropInnerMapX
    {
        [MapKey("utf8_stringX")]
        public string Utf8StringX { get; init; } = "";

        [Network]
        public Network? Network { get; init; }

        [MapKey("arrayX")]
        public LinkedList<long> ArrayX { get; init; } = new();
    }

    public class PropInnerMap
    {
        [MapKey("mapX")]
        public PropInnerMapX MapX { get; init; } = new();
    }

    public class PropInnerNonexistant
    {
        [Inject("injected")]
        public string Injected { get; init; } = "";

        [Network]
        public Network? Network { get; init; }
    }

    public class PropNonexistant
    {
        [MapKey("innerNonexistant", true)]
        public PropInnerNonexistant InnerNonexistant { get; init; } = new();

        [Inject("injected")]
        public string Injected { get; init; } = "";

        [Network]
        public Network? Network { get; init; }

        [Network]
        public Network? Network2 { get; init; }
    }

    public class PropTypeHolder
    {
        [MapKey("utf8_string")]
        public string Utf8String { get; init; } = "";

        [MapKey("bytes")]
        public byte[] Bytes { get; init; } = [];

        [MapKey("uint16")]
        public int Uint16 { get; init; }

        [MapKey("uint32")]
        public long Uint32 { get; init; }

        [MapKey("uint64")]
        public ulong Uint64 { get; init; }

        [MapKey("uint128")]
        public BigInteger Uint128 { get; init; }

        [MapKey("int32")]
        public int Int32 { get; init; }

        [MapKey("boolean")]
        public bool Boolean { get; init; }

        [MapKey("array")]
        public ICollection<long> Array { get; init; } = new List<long>();

        [MapKey("double")]
        public double Double { get; init; }

        [MapKey("float")]
        public float Float { get; init; }

        [MapKey("map")]
        public PropInnerMap Map { get; init; } = new();

        [MapKey("nonexistant", true)]
        public PropNonexistant Nonexistant { get; init; } = new();

        public string UnannotatedDefault { get; init; } = "should stay default";
    }

    // No-network variants for enumeration tests.

    public class PropNNInnerMapX
    {
        [MapKey("utf8_stringX")]
        public string Utf8StringX { get; init; } = "";

        [MapKey("arrayX")]
        public LinkedList<long> ArrayX { get; init; } = new();
    }

    public class PropNNInnerMap
    {
        [MapKey("mapX")]
        public PropNNInnerMapX MapX { get; init; } = new();
    }

    public class PropNNInnerNonexistant
    {
        [Inject("injected")]
        public string Injected { get; init; } = "";
    }

    public class PropNNNonexistant
    {
        [MapKey("innerNonexistant", true)]
        public PropNNInnerNonexistant InnerNonexistant { get; init; } = new();

        [Inject("injected")]
        public string Injected { get; init; } = "";
    }

    public class PropNoNetworkTypeHolder
    {
        [MapKey("utf8_string")]
        public string Utf8String { get; init; } = "";

        [MapKey("bytes")]
        public byte[] Bytes { get; init; } = [];

        [MapKey("uint16")]
        public int Uint16 { get; init; }

        [MapKey("uint32")]
        public long Uint32 { get; init; }

        [MapKey("uint64")]
        public ulong Uint64 { get; init; }

        [MapKey("uint128")]
        public BigInteger Uint128 { get; init; }

        [MapKey("int32")]
        public int Int32 { get; init; }

        [MapKey("boolean")]
        public bool Boolean { get; init; }

        [MapKey("array")]
        public ICollection<long> Array { get; init; } = new List<long>();

        [MapKey("double")]
        public double Double { get; init; }

        [MapKey("float")]
        public float Float { get; init; }

        [MapKey("map")]
        public PropNNInnerMap Map { get; init; } = new();

        [MapKey("nonexistant", true)]
        public PropNNNonexistant Nonexistant { get; init; } = new();
    }

#pragma warning disable CS0618 // Obsolete
    public class DeprecatedParameterTypeHolder
    {
        [Parameter("utf8_string")]
        public string Utf8String { get; init; } = "";

        [Parameter("double")]
        public double Double { get; init; }
    }
#pragma warning restore CS0618

    public class NoCtorNoAttributeType
    {
        public NoCtorNoAttributeType(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    public class ReadOnlyPropertyType
    {
        [MapKey("utf8_string")]
        public string Utf8String { get; } = "";
    }

    public class NoAnnotatedPropertiesType
    {
        public string Utf8String { get; init; } = "";
    }
}
