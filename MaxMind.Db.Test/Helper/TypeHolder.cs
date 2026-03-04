#region

using System.Collections.Generic;
using System.Numerics;

#endregion

namespace MaxMind.Db.Test.Helper
{
    public class InnerMapX
    {
        [Constructor]
        public InnerMapX(
            string utf8_stringX,
            [Network] Network network,
            LinkedList<long> arrayX
            )
        {
            ArrayX = arrayX;
            Network = network;
            Utf8StringX = utf8_stringX;
        }

        public LinkedList<long> ArrayX { get; set; }

        public Network Network { get; set; }

        public string Utf8StringX { get; set; }
    }

    public class InnerMap
    {
        [Constructor]
        public InnerMap(InnerMapX mapX)
        {
            MapX = mapX;
        }

        public InnerMapX MapX { get; set; }
    }

    public class InnerNonexistant
    {
        [Constructor]
        public InnerNonexistant(
            // To test these are set even if the parent and grandparent
            // don't exist in the database.
            [Inject("injected")] string injected,
            [Network] Network network
            )
        {
            Injected = injected;
            Network = network;
        }

        public string Injected { get; set; }
        public Network Network { get; }
    }

    public class Nonexistant
    {
        [Constructor]
        public Nonexistant(
            [MapKey("innerNonexistant", true)] InnerNonexistant innerNonexistant,
            // The next two test that they are set even if the parent map doesn't exist
            // in the database.
            [Inject("injected")] string injected,
            [Network] Network network,
            // Test that repeated network parameters work, or at least don't blow
            // up. Not sure why you would want to do this.
            [Network] Network network2
            )
        {
            Injected = injected;
            InnerNonexistant = innerNonexistant;
            Network = network;
            Network2 = network2;
        }

        public string Injected { get; set; }
        public InnerNonexistant InnerNonexistant { get; set; }
        public Network Network { get; }
        public Network Network2 { get; }
    }

    public class TypeHolder
    {
        [Constructor]
        public TypeHolder(
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
            [MapKey("map")] InnerMap map,
            [MapKey("nonexistant", true)] Nonexistant nonexistant
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

        public Nonexistant Nonexistant { get; set; }

        public ICollection<long> Array { get; set; }
        public bool Boolean { get; set; }

        public byte[] Bytes { get; set; }

        public double Double { get; set; }
        public float Float { get; set; }

        public InnerMap Map { get; set; }

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