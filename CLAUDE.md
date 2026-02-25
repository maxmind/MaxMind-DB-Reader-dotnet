# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**MaxMind-DB-Reader-dotnet** is the .NET API for reading MaxMind DB files. MaxMind DB is a binary file format that stores data indexed by IP address subnets (IPv4 or IPv6). This is the lower-level library used by the GeoIP2-dotnet client library.

**Key Technologies:**
- .NET 10.0, .NET 9.0, .NET 8.0, .NET Standard 2.1, and .NET Standard 2.0
- xUnit for testing
- BenchmarkDotNet for performance benchmarking
- Modern C# features (nullable reference types, unsafe code, Span<T>)

## Development Commands

### Building

```bash
# Build all projects
dotnet build MaxMind.Db
dotnet build MaxMind.Db.Test
dotnet build MaxMind.Db.Benchmark

# Build entire solution
dotnet build MaxMind.Db.sln
```

### Running Tests

```bash
# Run all tests
dotnet test MaxMind.Db.Test/MaxMind.Db.Test.csproj

# Run specific test class
dotnet test --filter "FullyQualifiedName~ReaderTest"
dotnet test --filter "FullyQualifiedName~DecoderTest"

# Run specific test method
dotnet test --filter "FullyQualifiedName~ReaderTest.TestMany"
```

### Running Benchmarks

```bash
# Run benchmarks (must specify target framework)
dotnet run -c Release -f net10.0 -p MaxMind.Db.Benchmark/MaxMind.Db.Benchmark.csproj
```

### Test Data Submodule

The test suite requires the MaxMind-DB test data git submodule:

```bash
# Initialize submodule (if not already done)
git submodule update --init --recursive

# Update submodule to latest
git submodule update --remote
```

Tests expect to find test databases in `MaxMind.Db.Test/TestData/MaxMind-DB/test-data/`.

## Code Architecture

### Core Components

The library has three main architectural layers:

1. **Buffer Layer** - File access abstraction
   - `Buffer` (abstract base): Defines read interface for binary data
   - `MemoryMapBuffer`: Memory-mapped file implementation (default, best balance)
   - `ArrayBuffer`: In-memory byte array implementation (fastest lookups, highest RAM usage)

2. **Reader Layer** - Database navigation and IP lookup
   - `Reader`: Main entry point for IP address lookups
   - Performs binary search tree traversal to locate data for an IP address
   - Supports three `FileAccessMode` options: `MemoryMapped`, `MemoryMappedGlobal`, `Memory`

3. **Decoder Layer** - Binary format deserialization
   - `Decoder`: Converts binary MaxMind DB format to .NET objects
   - `TypeActivatorCreator`: Compiles LINQ expression trees for fast object instantiation
   - `DictionaryActivatorCreator`, `ListActivatorCreator`: Specialized activators for collections

### MaxMind DB Binary Format

**File Structure:**
1. **Search Tree Section**: Binary search tree at beginning of file
2. **Data Section**: Deduplicated data records (multiple tree nodes can point to same data)
3. **Metadata Section**: Database metadata at end of file with magic marker (0xAB 0xCD 0xEF + "MaxMind.com")

**IP Lookup Process:**
1. Parse IP address (IPv4 = 32 bits, IPv6 = 128 bits)
2. Traverse binary search tree bit-by-bit from most significant to least significant
3. Each node contains two pointers (0 branch and 1 branch)
4. When data pointer found, resolve to data section and decode

**IPv4 Optimization:**
For IPv4 lookups in IPv6 databases, the Reader pre-calculates the IPv4 start node, skipping the first 96 nodes (::0/96 prefix).

### Deserialization System

The library uses an attribute-based deserialization system that maps MaxMind DB data to .NET types:

#### Four Key Attributes

1. **`[Constructor]`**: Marks the constructor to use for deserialization (one per class)

2. **`[Parameter("db_field_name")]`**: Maps database field to constructor parameter
   - Supports `AlwaysCreate = true` to instantiate nested objects even when database field is missing
   - If no attribute, uses parameter name as database key

3. **`[Inject("injectable_name")]`**: Injects runtime values not in database
   - Pass values via `InjectableValues` dictionary to `Find<T>()`
   - Example: Inject the queried IP address into the result object

4. **`[Network]`**: Injects the network CIDR (prefix length + network address) for the matched IP

#### Example Model Class

```csharp
using MaxMind.Db;

public class AsnResponse
{
    [Constructor]
    public AsnResponse(
        [Parameter("autonomous_system_number")] long? autonomousSystemNumber = null,
        [Parameter("autonomous_system_organization")] string? autonomousSystemOrganization = null,
        [Inject("ip_address")] IPAddress? ipAddress = null,
        [Network] Network? network = null
    )
    {
        AutonomousSystemNumber = autonomousSystemNumber;
        AutonomousSystemOrganization = autonomousSystemOrganization;
        IpAddress = ipAddress;
        Network = network;
    }

    public long? AutonomousSystemNumber { get; }
    public string? AutonomousSystemOrganization { get; }
    public IPAddress? IpAddress { get; }
    public Network? Network { get; }
}
```

### Performance Optimizations

**Compiled Activators:**
- Constructor delegates compiled once per type using LINQ Expressions
- Cached in `ConcurrentDictionary` for thread-safe reuse
- Much faster than `Activator.CreateInstance()` reflection

**Zero-Copy Reads:**
- `Key` struct avoids allocating strings for map keys (stores buffer offset + size + precomputed hash)
- `MemoryMapBuffer` uses unsafe pointer access for reading strings directly from memory-mapped regions
- `Span<byte>` usage on .NET Core for stack-allocated IP address processing

**Memory Management:**
- `ArrayPool<object?>` for parameter arrays (reduces GC pressure)
- LRU cache (`CachedDictionary`) for `FindAll()` enumeration
- Memory-mapped files avoid loading entire database into RAM

**Hot Path Inlining:**
- Size calculation methods marked for inlining (`CtrlData`, `DecodeSize`)
- Fast paths for common types (`Dictionary<string, string>`, `List<string>`)

### Threading and Concurrency

**Thread Safety:**
- `Reader` instances are **fully thread-safe** for concurrent reads
- **Recommended pattern**: Create one `Reader`, share across threads
- No mutable shared state in hot paths
- Buffer implementations use read-only operations

**Concurrent Data Structures:**
- `ConcurrentDictionary` for activator caching
- Mutex-based synchronization during memory-mapped file creation only
- No explicit locks in Reader/Decoder hot paths

**Test Coverage:**
- `ThreadingTest.cs` validates concurrent lookups return consistent results
- `TestManyOpens()` verifies safe concurrent Reader construction

## Working with This Codebase

### Adding New MaxMind DB Attributes

If adding new deserialization features:

1. Create attribute class inheriting from `System.Attribute`
2. Update `TypeActivatorCreator` to handle new attribute in parameter array building
3. Add tests in `DecoderTest.cs` or `ReaderTest.cs`
4. Update XML documentation for the new attribute

### Adding Support for New Data Types

When adding support for new MaxMind DB data types:

1. **Add ObjectType enum value** in `Decoder.cs`
2. **Implement DecodeByType case** for the new type
3. **Add test database** with new type to `MaxMind.Db.Test/TestData/` submodule
4. **Add unit tests** in `DecoderTest.cs`
5. **Update `releasenotes.md`** with the change

### Conditional Compilation for .NET Versions

The codebase supports multiple target frameworks with different capabilities:

```csharp
#if NET6_0_OR_GREATER
    // Use Span<T>, ReadOnlySpan<T>, etc.
#elif NETSTANDARD2_1
    // Use some newer APIs
#elif NETSTANDARD2_0
    // Fallback implementations
#endif
```

When using newer .NET APIs, ensure backward compatibility with .NET Standard 2.0/2.1.

### Performance-Critical Code

When modifying hot paths (`Decoder`, `Reader.FindAddressInTree`, `Buffer` implementations):

1. **Run benchmarks** before and after changes
2. **Profile allocations** - minimize GC pressure
3. **Consider unsafe code** for zero-copy operations (requires careful bounds checking)
4. **Benchmark across target frameworks** - optimizations may differ

### Unsafe Code Guidelines

This library uses `unsafe` code for performance in `MemoryMapBuffer`:

- **Always validate buffer bounds** before pointer access
- **Use checked arithmetic** when computing offsets
- **Document safety invariants** in comments
- **Test thoroughly** including edge cases (empty strings, large strings, etc.)

### Avoiding Breaking Changes

This library uses [Semantic Versioning](https://semver.org/):

- **Patch releases** (x.y.Z): Bug fixes only, no API changes
- **Minor releases** (x.Y.0): New features, backward compatible
- **Major releases** (X.0.0): Breaking changes allowed

For minor releases, maintain backward compatibility:
- Don't change existing public method signatures
- Don't remove public types or members
- New constructor parameters should have default values
- Mark deprecated APIs with `[Obsolete("message")]`

### releasenotes.md Format

Always update `releasenotes.md` for user-facing changes:

```markdown
## 4.3.0 (YYYY-MM-DD) ##

* Description of new feature or bug fix. Pull request by Author. GitHub #123.
* Breaking changes should be marked with **BREAKING** (major versions only).
```

## Common Patterns

### Pattern: FileAccessMode Selection

```csharp
// Default: MemoryMapped (good balance)
using var reader = new Reader("GeoIP2-City.mmdb");

// MemoryMappedGlobal (for cross-session sharing on Windows)
using var reader = new Reader("GeoIP2-City.mmdb", FileAccessMode.MemoryMappedGlobal);

// Memory (fastest lookups, highest RAM)
using var reader = new Reader("GeoIP2-City.mmdb", FileAccessMode.Memory);
```

### Pattern: IP Lookup with Injectables

```csharp
var ip = IPAddress.Parse("8.8.8.8");
var injectables = new InjectableValues();
injectables.AddValue("ip_address", ip);
var result = reader.Find<MyModel>(ip, injectables);
```

### Pattern: Enumerating All Records

```csharp
foreach (var record in reader.FindAll<MyModel>())
{
    // Process record
    // Note: Uses LRU cache to avoid re-decoding recently seen networks
}
```

### Pattern: Async Database Loading

```csharp
// For Memory mode, can load asynchronously
using var reader = await Reader.CreateAsync("GeoIP2-City.mmdb", FileAccessMode.Memory);
```

## Code Quality

### Static Analysis

The project enforces strict code quality:

- **EnforceCodeStyleInBuild**: Code style violations are build errors
- **TreatWarningsAsErrors**: All warnings must be resolved
- **EnableNETAnalyzers**: .NET code analyzers enabled
- **.editorconfig**: Defines consistent coding style
- **AnalysisLevel**: Set to `latest` for most up-to-date analyzers

### Code Style

Follow the `.editorconfig` settings:
- **Indentation**: 4 spaces
- **Line endings**: CRLF (Windows-style)
- **var usage**: Prefer `var` everywhere
- **Expression bodies**: Use for accessors and properties, not constructors or methods
- **Braces**: Always use braces for control flow
- **Namespace declarations**: Block-scoped (`namespace Foo { }`)
- **Field naming**: Prefix private fields with underscore (`_fieldName`)

### XML Documentation

All public types and members must have XML documentation:

```csharp
/// <summary>
/// Brief description of what this does.
/// </summary>
/// <param name="paramName">Description of parameter.</param>
/// <returns>Description of return value.</returns>
/// <exception cref="ExceptionType">When this exception is thrown.</exception>
public ReturnType MethodName(ParamType paramName)
{
    // Implementation
}
```

## Version Requirements

- **Target Frameworks**: net10.0, net9.0, net8.0, netstandard2.1, netstandard2.0
- **Language Version**: C# 14.0
- **Key Dependencies**:
  - `xunit.v3`: 3.0.0 (testing framework)
  - `BenchmarkDotNet`: For performance benchmarking
- **AllowUnsafeBlocks**: true (required for zero-copy string reads)

## Additional Context

### Relationship to GeoIP2-dotnet

This library is the lower-level component used by MaxMind's GeoIP2-dotnet library:

- **MaxMind-DB-Reader-dotnet** (this repo): Generic MaxMind DB file reader
- **GeoIP2-dotnet**: Higher-level library with GeoIP2-specific models and web service client

For GeoIP2 use cases, consumers typically use GeoIP2-dotnet, which depends on this library.

### When to Use This Library Directly

Use MaxMind-DB-Reader-dotnet directly when:
- Reading custom MaxMind DB databases (not GeoIP2)
- Need maximum control over deserialization
- Want to avoid GeoIP2-specific dependencies

### Test Data Submodule

The `MaxMind.Db.Test/TestData/MaxMind-DB` directory is a git submodule containing test databases:
- Always update this submodule before running tests
- When adding tests for new features, may need to update submodule to get test databases with new fields
- See https://github.com/maxmind/MaxMind-DB for test database format specifications

## Additional Resources

- [MaxMind DB Format Specification](https://maxmind.github.io/MaxMind-DB/)
- [GeoIP2-dotnet Documentation](https://maxmind.github.io/GeoIP2-dotnet/)
- GitHub Issues: https://github.com/maxmind/MaxMind-DB-Reader-dotnet/issues

---

*Last Updated: 2025-11-19*
