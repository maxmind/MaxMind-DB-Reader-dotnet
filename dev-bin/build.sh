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
  dotnet run -f $CONSOLE_FRAMEWORK -c $CONFIGURATION -p ./MaxMind.Db.Benchmark/MaxMind.Db.Benchmark.csproj

  # Running Unit Tests
  dotnet test -f $CONSOLE_FRAMEWORK -c $CONFIGURATION ./MaxMind.Db.Test/MaxMind.Db.Test.csproj

else

  echo Using Mono

  msbuild /t:restore MaxMind.Db.sln

  msbuild /t:build /p:Configuration=$CONFIGURATION /p:TargetFramework=net452 ./MaxMind.Db.Test/MaxMind.Db.Test.csproj

  nuget install xunit.runner.console -ExcludeVersion -Version 2.2.0 -OutputDirectory .
  mono ./xunit.runner.console/tools/xunit.console.exe ./MaxMind.Db.Test/bin/$CONFIGURATION/net452/MaxMind.Db.Test.dll -notrait "Category=BreaksMono"

fi
