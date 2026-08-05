using System.Collections.Generic;
using MaxMind.Db;

namespace MaxMind.Db.NativeAot.Models
{
    public abstract record NamedEntity
    {
        [MapKey("names")]
        public IReadOnlyDictionary<string, string> Names { get; init; } =
            new Dictionary<string, string>();

        [Inject("locales")]
        public IReadOnlyList<string> Locales { get; init; } = ["en"];

        public string? Name
        {
            get
            {
                foreach (var locale in Locales)
                {
                    if (Names.TryGetValue(locale, out var name))
                    {
                        return name;
                    }
                }
                return null;
            }
        }
    }

    public sealed record City : NamedEntity;

    public sealed record Subdivision : NamedEntity;

    public sealed record Traits
    {
        [Network]
        public Network? Network { get; init; }
    }

    public abstract record CityResponseBase
    {
        [MapKey("city", true)]
        public City City { get; init; } = new();

        [MapKey("subdivisions")]
        public IReadOnlyList<Subdivision> Subdivisions { get; init; } = [];

        [MapKey("traits", true)]
        public Traits Traits { get; init; } = new();
    }

    public sealed record CityResponse : CityResponseBase;

    public sealed class DecoderConstructorModel
    {
        [Constructor]
        public DecoderConstructorModel(
            [MapKey("utf8_string")] string utf8String,
            [MapKey("array")] IReadOnlyList<long> array,
            [MapKey("map")] IReadOnlyDictionary<string, object> map
            )
        {
            Utf8String = utf8String;
            Array = array;
            Map = map;
        }

        public IReadOnlyList<long> Array { get; }
        public IReadOnlyDictionary<string, object> Map { get; }
        public string Utf8String { get; }
    }

    public abstract record DecoderPropertyModelBase
    {
        [MapKey("array")]
        public ICollection<long> Array { get; init; } = new List<long>();

        [MapKey("map")]
        public Dictionary<string, object> Map { get; init; } = new();
    }

    public sealed record DecoderPropertyModel : DecoderPropertyModelBase;

    public sealed record DecoderConcreteCollectionModel
    {
        [MapKey("array")]
        public LinkedList<long> Array { get; init; } = new();

        [MapKey("map")]
        public ConcreteDictionary<string, object> Map { get; init; } = new();
    }

    public sealed class ConcreteDictionary<TKey, TValue> : Dictionary<TKey, TValue>
        where TKey : notnull
    {
    }

    // Deriving from MapKeyAttribute is a shape the generator cannot evaluate, so
    // ReflectionFallbackModel is deliberately left without a generated registration.
    // It is what pins the behavior of the reflection fallback under a real NativeAOT
    // publish, which no analyzer covers: IL3050 is not reported for the
    // Expression.Compile calls that path relies on.
    public sealed class Utf8KeyAttribute : MapKeyAttribute
    {
        public Utf8KeyAttribute() : base("utf8_string")
        {
        }
    }

    // MMDBSG011 is suppressed here rather than project-wide, and only because this
    // model exists to exercise the skip. A project-wide NoWarn would hide real
    // diagnostics in the rest of the sample.
#pragma warning disable MMDBSG011
    public sealed record ReflectionFallbackModel
    {
        [Utf8Key]
        public string? Utf8String { get; init; }
    }
#pragma warning restore MMDBSG011
}
