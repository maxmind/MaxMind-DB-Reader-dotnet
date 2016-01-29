# MaxMind DB Reader #

## Description ##

This is the .NET API for reading MaxMind DB files. MaxMind DB is a binary file
format that stores data indexed by IP address subnets (IPv4 or IPv6).

## Requirements ##

This library works with .NET Framework version 4.5 and above.

## Installation ##

### NuGet ###

We recommend installing this library with NuGet. To do this, type the
following into the Visual Studio Package Manager Console:

```
install-package MaxMind.Db
```

## Usage ##

*Note:* For accessing MaxMind GeoIP2 databases, we generally recommend using
the GeoIP2 .NET API rather than using this package directly.

To use the API, you must first create a `Reader` object. The constructor for
the reader object takes a `string` with the path to the MaxMind DB file.
Optionally you may pass a second parameter with a `FileAccessMode` enum with
the value `MemoryMapped` or `Memory`. The default mode is `MemoryMapped`,
which maps the file to virtual memory. This often provides performance
comparable to loading the file into real memory with the `Memory`  mode while
using significantly less memory.

To look up an IP address, pass a `string` representing the IP address to the
`Find<T>` method on `Reader`. This method will return the result as type `T`.
`T` may either be a generic collection or a class using the
`[MaxMind.Db.Constructor]` attribute to declare which constructor to use
during deserialization and the `[MaxMind.Db.Parameter("name")]` to map the
database key `name` to a particular constructor parameter.

We recommend reusing the `Reader` object rather than creating a new one for
each lookup. The creation of this object is relatively expensive as it must
read in metadata for the file.

## Example ##

```csharp

using (var reader = new Reader("GeoIP2-City.mmdb"))
{
    var data = reader.Find<Dictionary<string, object>>("24.24.24.24");
    ...
}
```

## Multi-Threaded Use ##

This API fully supports use in multi-threaded applications. In such
applications, we suggest creating one `Reader` object and sharing that among
threads.

## Format ##

The MaxMind DB format is an open format for quickly mapping IP addresses to
records. See [the specification]
(https://github.com/maxmind/MaxMind-DB/blob/master/MaxMind-DB-spec.md)
for more information on the format.

## Bug Tracker ##

Please report all issues with this code using the [GitHub issue tracker]
(https://github.com/maxmind/MaxMind-DB-Reader-dotnet/issues).

If you are having an issue with a MaxMind database or service that is not
specific to this reader, please [contact MaxMind support]
(http://www.maxmind.com/en/support).

## Contributing ##

Patches and pull requests are encouraged. Please include unit tests whenever
possible.

## Versioning ##

The MaxMind DB Reader API uses [Semantic Versioning](http://semver.org/).

## Copyright and License ##

This software is Copyright (c) 2016 by MaxMind, Inc.

This is free software, licensed under the Apache License, Version 2.0.
