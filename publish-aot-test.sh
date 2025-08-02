#!/bin/bash
set -e

echo "Building MaxMind.Db library for net9.0..."
dotnet build MaxMind.Db/MaxMind.Db.csproj -c Release -f net9.0

echo "Publishing test suite with NativeAOT..."
dotnet publish MaxMind.Db.Test/MaxMind.Db.Test.csproj \
  -c Release \
  -f net9.0 \
  -r linux-x64 \
  --self-contained \
  -p:PublishAot=true \
  -o ./aot-test-output

echo "AOT build complete. Output in ./aot-test-output"
ls -lh ./aot-test-output/MaxMind.Db.Test 2>/dev/null || echo "Binary not found"