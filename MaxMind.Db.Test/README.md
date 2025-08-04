# Testing Source Generators vs Reflection

This test project includes infrastructure to verify that both source generator and reflection activation paths work correctly and produce identical results.

## Test Modes

The test project supports three modes:

1. **All** (default) - Runs all tests with both source generator and reflection wrappers
2. **SourceGenerator** - Runs only source generator tests 
3. **Reflection** - Runs only reflection tests (source generators disabled)

## Running Tests

### Run all tests (default)
```bash
dotnet test
```

### Run only source generator tests
```bash
dotnet test -p:TestMode=SourceGenerator
```

### Run only reflection tests  
```bash
dotnet test -p:TestMode=Reflection
```

## Test Architecture

### ReaderWrapper Pattern

The testing uses an abstract `ReaderWrapper` class with two implementations:

- `SourceGeneratorReaderWrapper` - Uses normal Reader with source generators enabled
- `ReflectionReaderWrapper` - Temporarily disables source generators to force reflection

### Test Classes

- `SourceGeneratorReaderTests` - Tests that verify source generator path works
- `ReflectionReaderTests` - Tests that verify reflection path works  
- `ParametrizedReaderTests` - Tests that run with both wrappers to ensure identical behavior

### Conditional Compilation

Tests are conditionally compiled based on the TestMode:

- `TEST_SOURCE_GENERATOR_ONLY` - Only includes source generator tests
- `TEST_REFLECTION_ONLY` - Only includes reflection tests
- No symbol (default) - Includes all tests

## Implementation Details

The `ReflectionReaderWrapper` works by temporarily clearing registered source generator activators during test execution, forcing the system to fall back to reflection-based activation. This ensures we're actually testing the reflection path even when source generators are available.

The wrapper pattern allows the same test logic to run against both activation methods, ensuring they produce identical results.