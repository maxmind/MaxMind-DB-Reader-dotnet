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
}
