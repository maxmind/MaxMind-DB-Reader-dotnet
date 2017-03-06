#!/usr/bin/env bash

cd `dirname $0`/..

if [ -n "$DOTNETCORE" ]; then

  echo Using .NET CLI

  if [[ "$TRAVIS_OS_NAME" == "osx" ]]; then
    # This is due to: https://github.com/NuGet/Home/issues/2163#issue-135917905
    echo "current ulimit is: `ulimit -n`..."
    ulimit -n 1024
    echo "new limit: `ulimit -n`"
  fi

  dotnet restore

  # Building the dependent projects such as MaxMind.Db.Benchmark
  # and MaxMind.Db.Test will build the MaxMind.Db lib; no need to
  # build it explicitly (with dotnet-build command).

  # Running Benchmark
  dotnet run -f netcoreapp1.0 -c $CONFIGURATION -p ./MaxMind.Db.Benchmark/MaxMind.Db.Benchmark.csproj

  # Running Unit Tests
  dotnet test -f netcoreapp1.0 -c $CONFIGURATION ./MaxMind.Db.Test/MaxMind.Db.Test.csproj

else

  echo Using Mono

  nuget restore

  xbuild /p:Configuration=$CONFIGURATION mono/MaxMind.Db.Mono.sln

  mono ./packages/xunit.runner.console.2.2.0/tools/xunit.console.exe ./mono/bin/$CONFIGURATION/MaxMind.Db.Test.dll -notrait "Category=BreaksMono"

fi
