#region

using System.Collections.Generic;
using System.Numerics;

#endregion

namespace MaxMind.Db.Test.Helper
{
    public class InnerMapX
    {
        [MaxMindDbConstructor]
        public InnerMapX(
            string utf8_stringX,
            IList<long> arrayX
            )
        {
            ArrayX = arrayX;
            Utf8StringX = utf8_stringX;
        }

        public IList<long> ArrayX { get; set; }

        public string Utf8StringX { get; set; }
    }

    public class InnerMap
    {
        [MaxMindDbConstructor]
        public InnerMap(InnerMapX mapX)
        {
            MapX = mapX;
        }

        public InnerMapX MapX { get; set; }
    }

    public class TypeHolder
    {
        [MaxMindDbConstructor]
        public TypeHolder(
            string utf8_string,
            byte[] bytes,
            int uint16,
            long uint32,
            ulong uint64,
            BigInteger uint128,
            int int32,
            bool boolean,
            IList<long> array,
            [MaxMindDbProperty("double")] double mmDouble,
            [MaxMindDbProperty("float")] float mmFloat,
            [MaxMindDbProperty("map")] InnerMap map
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
        }

        public IList<long> Array { get; set; }
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