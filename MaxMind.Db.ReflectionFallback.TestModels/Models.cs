using System.Collections.Generic;

namespace MaxMind.Db.ReflectionFallback.TestModels
{
    public sealed class FallbackList<T> : List<T>
    {
    }

    public sealed class ReflectionConstructorModel
    {
        [Constructor]
        public ReflectionConstructorModel(
            [MapKey("utf8_string")] string utf8String,
            [MapKey("array")] FallbackList<long> values
            )
        {
            Utf8String = utf8String;
            Values = values;
        }

        public string Utf8String { get; }
        public FallbackList<long> Values { get; }
    }

    public sealed class ReflectionPropertyModel
    {
        [MapKey("missing")]
        public string Missing { get; set; } = "preserved default";

        [MapKey("utf8_string")]
        public string? Utf8String { get; set; }

        [MapKey("array")]
        public FallbackList<long>? Values { get; set; }
    }

    public sealed class ReflectionInnerModel
    {
        [MapKey("utf8_stringX")]
        public string? Value { get; set; }
    }

    public sealed class ReflectionAlwaysCreateConstructorModel
    {
        [Constructor]
        public ReflectionAlwaysCreateConstructorModel(
            [MapKey("no_such_key", true)] long absentValueType,
            [MapKey("no_such_map", true)] ReflectionInnerModel absentModel
            )
        {
            AbsentValueType = absentValueType;
            AbsentModel = absentModel;
        }

        public ReflectionInnerModel AbsentModel { get; }
        public long AbsentValueType { get; }
    }

    public sealed class ReflectionAlwaysCreatePropertyModel
    {
        [MapKey("no_such_map", true)]
        public ReflectionInnerModel? AbsentModel { get; set; }

        [MapKey("no_such_key", true)]
        public long AbsentValueType { get; set; }
    }
}
