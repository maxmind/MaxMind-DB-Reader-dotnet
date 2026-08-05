# MaxMind DB Reader

[![NuGet](https://img.shields.io/nuget/v/MaxMind.Db)](https://www.nuget.org/packages/MaxMind.Db)

## Description

This is the .NET API for reading MaxMind DB files. MaxMind DB is a binary file
format that stores data indexed by IP address subnets (IPv4 or IPv6).

## Installation

### NuGet

We recommend installing this library with NuGet. To do this, type the following
into the Visual Studio Package Manager Console:

```
install-package MaxMind.Db
```

## Usage

_Note:_ For accessing MaxMind GeoIP databases, we generally recommend using the
GeoIP .NET API rather than using this package directly.

To use the API, you must first create a `Reader` object. The constructor for the
reader object takes a `string` with the path to the MaxMind DB file. Optionally
you may pass a second parameter with a `FileAccessMode` enum with the value
`MemoryMapped` or `Memory`. The default mode is `MemoryMapped`, which maps the
file to virtual memory. This often provides performance comparable to loading
the file into real memory with the `Memory` mode while using significantly less
memory.

To look up an IP address, pass a `System.Net.IPAddress` object to the `Find<T>`
method on `Reader`. This method will return the result as type `T`. `T` may
either be a generic collection, a class using the `[MaxMind.Db.Constructor]`
attribute to declare which constructor to use during deserialization, or a class
with `[MaxMind.Db.MapKey("name")]`-annotated `init` properties for
property-based activation.

We recommend reusing the `Reader` object rather than creating a new one for each
lookup. The creation of this object is relatively expensive as it must read in
metadata for the file.

## Example Decoding to a Dictionary

```csharp

using (var reader = new Reader("GeoIP2-City.mmdb"))
{
    var ip = IPAddress.Parse("24.24.24.24");
    var data = reader.Find<Dictionary<string, object>>(ip);
    ...
}
```

## Example Decoding to a Model Class (Constructor-Based)

```csharp
using MaxMind.Db;
using System.Net;

namespace MyCode
{
    public class Asn
    {
        [Constructor]
        public Asn(
            // The MapKey attribute tells the reader to map the database
            // key to the specified constructor parameter or property.
            [MapKey("autonomous_system_number")] long? autonomousSystemNumber,
            [MapKey("autonomous_system_organization")] string autonomousSystemOrganization,

            // The Inject attribute allows you to inject arbitrary values
            // when deserializing.
            [Inject("ip_address")] IPAddress ipAddress,

            // The Network attribute tells the reader to set the constructor
            // parameter to be the network associated with the record in the
            // database.
            [Network] Network network)
        {
          ...
        }

        ...
    }


    public class Program
    {
        private static void Main(string[] args)
        {
            using (var reader = new Reader("GeoLite2-ASN.mmdb"))
            {
                var ip = IPAddress.Parse("24.24.24.24");
                var injectables = new InjectableValues();
                injectables.AddValue("ip_address", ip);
                var data = reader.Find<Asn>(ip, injectables);
                ...
            }
        }
    }
}
```

## Example Decoding to a Model Class (Property-Based)

As an alternative to constructor-based activation, you can use `init`
properties. This does not require a `[Constructor]`-annotated constructor.

```csharp
using MaxMind.Db;
using System.Net;

namespace MyCode
{
    public class Asn
    {
        [MapKey("autonomous_system_number")]
        public long? AutonomousSystemNumber { get; init; }

        [MapKey("autonomous_system_organization")]
        public string? AutonomousSystemOrganization { get; init; }

        [Inject("ip_address")]
        public IPAddress? IpAddress { get; init; }

        [Network]
        public Network? Network { get; init; }
    }

    public class Program
    {
        private static void Main(string[] args)
        {
            using (var reader = new Reader("GeoLite2-ASN.mmdb"))
            {
                var ip = IPAddress.Parse("24.24.24.24");
                var injectables = new InjectableValues();
                injectables.AddValue("ip_address", ip);
                var data = reader.Find<Asn>(ip, injectables);
                ...
            }
        }
    }
}
```

## Multi-Threaded Use

This API fully supports use in multi-threaded applications. In such
applications, we suggest creating one `Reader` object and sharing that among
threads.

## NativeAOT and Trimming

The `MaxMind.Db` NuGet package includes a C# source generator that enables
trim-safe, reflection-free deserialization for NativeAOT applications. The
generator is included automatically; no additional package or registration is
required. It needs the .NET SDK 7.0.100 or later; an older SDK reports `CS9057`
and skips the generator, leaving models on the reflection fallback. The
generator reports a diagnostic for any annotated model it cannot generate, in
whichever project declares that model.

The generator supports both model styles shown above:

- A non-generic model with exactly one accessible `[Constructor]`-annotated
  constructor.
- A non-generic property-based model with an accessible parameterless
  constructor and accessible annotated getters and setters. Attributes on
  inherited properties are supported, including concrete records whose
  annotations are declared on an abstract base record.

Generated collection activation supports common generic interfaces such as
`ICollection<T>`, `IReadOnlyList<T>`, `IDictionary<TKey, TValue>`, and
`IReadOnlyDictionary<TKey, TValue>`. Concrete collection and dictionary types
are supported when they implement the corresponding mutable interface and have
an accessible parameterless constructor; this includes types such as
`LinkedList<T>`. The generator discovers these types when they are model members
or closed generic arguments in direct `Reader.Find<T>` and `Reader.FindAll<T>`
calls.

There are several current limitations:

- Source generation is supported for C# models. Other .NET languages continue to
  use the reflection fallback, which is not guaranteed to work after trimming or
  with NativeAOT.
- Source generation requires C# 9 or later because generated registrations use
  module initializers. Earlier C# versions continue to use the reflection
  fallback in non-AOT builds.
- A generic wrapper around `Find<T>` or `FindAll<T>` is fine for models. Models
  are registered from their declarations, not from lookup sites, so a method
  like `T Lookup<T>(Reader reader, IPAddress address)` still resolves generated
  activation for every model declared in a generator-enabled project.

  What such a wrapper cannot carry is a **collection** result type. Collection
  and dictionary types have no annotated declaration to find, so they are
  discovered from the lookup site, and a wrapper hides which one is used. Use a
  concrete type argument at the call site — `Find<Dictionary<string, object>>`
  rather than `Lookup<Dictionary<string, object>>` — or make the collection a
  member of a model. The same applies to a result type chosen at run time.

  No diagnostic is reported for a wrapper, because the generator cannot tell
  from the call site whether the eventual type argument is a registered model or
  an unregistered collection, and warning on every wrapper would be a false
  positive for the common case. A constructed type that still contains a type
  parameter, such as `Find<Dictionary<string, T>>`, is reported as `MMDBSG015`.

- Generic model classes are not supported, closed or otherwise. Models are
  discovered from their declarations, so the generator only ever sees the
  unbound definition and reports `MMDBSG004`, even where every use is a closed
  construction such as `Find<Wrapper<string>>`.
- Models must be classes or records. Annotated structs and record structs are
  reported as `MMDBSG012`. A constructor-based struct then falls back to
  reflection and works; a property-based struct or record struct fails at run
  time, in a plain JIT build as much as under NativeAOT, because reflection does
  not surface a struct's implicit parameterless constructor and there is nothing
  to activate unless one is declared explicitly.
- MMDB array values cannot be deserialized into CLR array model members. Use a
  supported generic collection instead. `byte[]` remains supported for MMDB byte
  values.
- Private or protected model constructors, types, property getters, and property
  setters cannot be called by the generated code. Use `public` or `internal`
  accessibility.
- Models with `required` members must mark the constructor used for
  deserialization with `SetsRequiredMembersAttribute`. For property models, this
  is the accessible parameterless constructor.

Treat these diagnostics as build errors rather than suppressing them; each one
means a model would fall back to reflection, which is not guaranteed to work
after trimming or with NativeAOT. They are reported by default, including in a
model class library that knows nothing about how it will be published. That is
deliberate: an application's `PublishAot` does not propagate across a
`ProjectReference`, so keying the diagnostics off it would silence them in the
one compilation that can report them. To turn them off in a project that will
never be trimmed:

```xml
<PropertyGroup>
  <MaxMindDbAotDiagnostics>false</MaxMindDbAotDiagnostics>
</PropertyGroup>
```

Because that property decides whether the diagnostics are produced at all,
setting `dotnet_diagnostic.MMDBSG0NN.severity` in `.editorconfig` has no effect
once it is `false`.

A separately packaged model library must be built or rebuilt with a version of
`MaxMind.Db` that includes the source generator. Updating only the application
cannot add registrations to an already compiled model assembly; precompiled
model libraries without generated registrations are not guaranteed to work after
trimming or with NativeAOT. The absence of a source-generator diagnostic does
not validate models from an already-compiled referenced assembly. If that
assembly has no generated registrations, its reflection fallback is unsupported
and may fail at run time after trimming or with NativeAOT.

## Format

The MaxMind DB format is an open format for quickly mapping IP addresses to
records. See
[the specification](https://github.com/maxmind/MaxMind-DB/blob/main/MaxMind-DB-spec.md)
for more information on the format.

## Bug Tracker

Please report all issues with this code using the
[GitHub issue tracker](https://github.com/maxmind/MaxMind-DB-Reader-dotnet/issues).

If you are having an issue with a MaxMind database or service that is not
specific to this reader, please
[contact MaxMind support](https://support.maxmind.com/knowledge-base).

## Contributing

Patches and pull requests are encouraged. Please include unit tests whenever
possible.

## Versioning

The MaxMind DB Reader API uses [Semantic Versioning](https://semver.org/).

## Copyright and License

This software is Copyright (c) 2013-2026 by MaxMind, Inc.

This is free software, licensed under the Apache License, Version 2.0.
