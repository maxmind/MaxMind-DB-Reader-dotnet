#!/usr/bin/env bash

cd `dirname $0`/..

if [ -n "$DOTNETCORE" ]; then

  echo Using .NET CLI

  dotnet restore

  # Building the dependent projects such as MaxMind.Db.Benchmark
  # and MaxMind.Db.Test will build the MaxMind.Db lib; no need to
  # build it explicitly (with dotnet-build command).

  # Running Benchmark
  dotnet run -f netcoreapp1.0 -c $CONFIGURATION -p ./MaxMind.Db.Benchmark

  # Running Unit Tests
  dotnet test -f netcoreapp1.0 -c $CONFIGURATION ./MaxMind.Db.Test

else

  echo Using Mono

  cd mono

  nuget restore

  xbuild /p:Configuration=$CONFIGURATION

  mono ../mono/packages/NUnit.ConsoleRunner.3.4.1/tools/nunit3-console.exe --where "cat != BreaksMono" ./bin/$CONFIGURATION/MaxMind.Db.Test.dll

fi
