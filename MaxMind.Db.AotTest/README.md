# MaxMind.Db AOT Test

This project tests the NativeAOT compatibility of the MaxMind.Db library.

## Running the Tests

### Build and publish for NativeAOT:
```bash
dotnet publish -c Release -r linux-x64
```

Or for other platforms:
```bash
dotnet publish -c Release -r win-x64
dotnet publish -c Release -r osx-x64
```

### Run the test:
```bash
# Without database file (basic tests only)
./bin/Release/net9.0/linux-x64/publish/MaxMind.Db.AotTest

# With database file (full tests)
./bin/Release/net9.0/linux-x64/publish/MaxMind.Db.AotTest path/to/GeoLite2-City.mmdb
```

## Expected Output

When running successfully, you should see:
```
MaxMind.Db NativeAOT Compatibility Test
========================================
Test 1 - AOT Mode Detection: Running in AOT mode ✓
Test 2 - Type Creation: Basic type creation works ✓
Test 3 - Database Reading (GeoLite2-City.mmdb): 
  Database type: GeoLite2-City
  IP version: 6
  Record size: 28
  Node count: 3749418
  Found data for 8.8.8.8: 10 fields ✓

All tests passed successfully!
```

## Verifying AOT Benefits

Check the binary size and startup time:
```bash
# Check binary size
ls -lh bin/Release/net9.0/linux-x64/publish/MaxMind.Db.AotTest

# Measure startup time
time ./bin/Release/net9.0/linux-x64/publish/MaxMind.Db.AotTest
```

The AOT binary should:
- Start in < 10ms (vs ~100ms for JIT)
- Be a single self-contained executable
- Use less memory at runtime
- Have predictable performance from the first request