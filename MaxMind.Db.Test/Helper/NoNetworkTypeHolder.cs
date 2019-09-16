#region

using System.Collections.Generic;
using System.Numerics;

#endregion

namespace MaxMind.Db.Test.Helper
{
    public class NNInnerMapX
    {
        [Constructor]
        public NNInnerMapX(
            string utf8_stringX,
            LinkedList<long> arrayX
            )
        {
            ArrayX = arrayX;
            Utf8StringX = utf8_stringX;
        }

        public LinkedList<long> ArrayX { get; set; }

        public string Utf8StringX { get; set; }
    }

    public class NNInnerMap
    {
        [Constructor]
        public NNInnerMap(NNInnerMapX mapX)
        {
            MapX = mapX;
        }

        public NNInnerMapX MapX { get; set; }
    }

    public class NNInnerNonexistant
    {
        [Constructor]
        public NNInnerNonexistant(
            // To test this is set even if the parent and grandparent
            // don't exist in the database.
            [Inject("injected")] string injected
            )
        {
            Injected = injected;
        }

        public string Injected { get; set; }
    }

    public class NNNonexistant
    {
        [Constructor]
        public NNNonexistant(
            [Parameter("innerNonexistant", true)] NNInnerNonexistant innerNonexistant,
            // To test this is set even if the parent map doesn't exist
            // in the database.
            [Inject("injected")] string injected
            )
        {
            Injected = injected;
            InnerNonexistant = innerNonexistant;
        }

        public string Injected { get; set; }
        public NNInnerNonexistant InnerNonexistant { get; set; }
    }

    public class NoNetworkTypeHolder
    {
        [Constructor]
        public NoNetworkTypeHolder(
            string utf8_string,
            byte[] bytes,
            int uint16,
            long uint32,
            ulong uint64,
            BigInteger uint128,
            int int32,
            bool boolean,
            ICollection<long> array,
            [Parameter("double")] double mmDouble,
            [Parameter("float")] float mmFloat,
            [Parameter("map")] NNInnerMap map,
            [Parameter("nonexistant", true)] NNNonexistant nonexistant
            )
        {
            Array = array;
            Boolean = boolean;
            Bytes = bytes;
            Double = mmDouble;
            Float = mmFloat;

            Int32 = int32;
            Map = map;

            Uint16 = uint16;
            Uint32 = uint32;
            Uint64 = uint64;
            Uint128 = uint128;

            Utf8String = utf8_string;

            Nonexistant = nonexistant;
        }

        public NNNonexistant Nonexistant { get; set; }

        public ICollection<long> Array { get; set; }
        public bool Boolean { get; set; }

        public byte[] Bytes { get; set; }

        public double Double { get; set; }
        public float Float { get; set; }

        public NNInnerMap Map { get; set; }

        public long Int32 { get; set; }
        public int Uint16 { get; set; }
        public long Uint32 { get; set; }
        public ulong Uint64 { get; set; }
        public BigInteger Uint128 { get; set; }

        public string Utf8String { get; set; }

        public override string ToString()
        {
            return
                $"Boolean: {Boolean}, Bytes: {Bytes}, Int32: {Int32}, Uint16: {Uint16}, Uint32: {Uint32}, Uint64: {Uint64}, Uint128: {Uint128}, Utf8String: {Utf8String}";
        }
    }
}