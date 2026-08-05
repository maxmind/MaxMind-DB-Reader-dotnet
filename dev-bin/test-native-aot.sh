#!/usr/bin/env bash

set -euo pipefail

readonly native_aot_rid="${1:?usage: test-native-aot.sh <runtime-identifier>}"
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly repository_root
readonly library_project="$repository_root/MaxMind.Db/MaxMind.Db.csproj"
readonly app_project="$repository_root/MaxMind.Db.NativeAot/App/MaxMind.Db.NativeAot.App.csproj"
readonly netstandard_models_project="$repository_root/MaxMind.Db.NetStandard.TestModels/MaxMind.Db.NetStandard.TestModels.csproj"
readonly package_directory="$repository_root/artifacts/native-aot-packages"

# Derived rather than hard-coded so this cannot drift from the library, and read
# with -getProperty so the same value is used for the pack and for the reference.
version_prefix="$(dotnet msbuild "$library_project" \
    -getProperty:VersionPrefix -p:TargetFramework=net8.0)"
readonly version_prefix
readonly package_version="$version_prefix-aot-ci"

target_framework="$(dotnet msbuild "$app_project" -getProperty:TargetFramework)"
readonly target_framework

restore_directory="$(mktemp -d)"
readonly restore_directory
cleanup() {
    rm -rf "$restore_directory"
}
trap cleanup EXIT

# A previous run's package has the same file name, and NuGet would happily restore
# the stale one from this folder.
rm -rf "$package_directory"
mkdir -p "$package_directory"

dotnet pack "$library_project" \
    --configuration Release \
    --output "$package_directory" \
    -p:PackageVersion="$package_version"

# EmitCompilerGeneratedFiles lets the build assert the generator actually ran for a
# package consumer, which the app's own model project already checks for itself.
dotnet build "$netstandard_models_project" \
    --configuration Release \
    -p:MaxMindDbPackageVersion="$package_version" \
    -p:RestoreAdditionalProjectSources="$package_directory" \
    -p:RestorePackagesPath="$restore_directory" \
    -p:RestoreNoCache=true \
    -p:EmitCompilerGeneratedFiles=true

netstandard_generated_count="$(find "$repository_root/MaxMind.Db.NetStandard.TestModels/obj" \
    -name 'MaxMind.Db.SourceGenerator.g.cs' | wc -l)"
if [[ "$netstandard_generated_count" -eq 0 ]]; then
    echo "The source generator produced no registrations for the .NET Standard" \
        "consumer. Its models would fall back to reflection." >&2
    exit 1
fi

dotnet publish "$app_project" \
    --configuration Release \
    --runtime "$native_aot_rid" \
    -p:MaxMindDbPackageVersion="$package_version" \
    -p:RestoreAdditionalProjectSources="$package_directory" \
    -p:RestorePackagesPath="$restore_directory" \
    -p:RestoreNoCache=true

readonly publish_directory="$repository_root/MaxMind.Db.NativeAot/App/bin/Release/$target_framework/$native_aot_rid/publish"
if [[ ! -d "$publish_directory" ]]; then
    echo "No publish output at $publish_directory." >&2
    exit 1
fi

if [[ "$native_aot_rid" == win-* ]]; then
    "$publish_directory/MaxMind.Db.NativeAot.App.exe"
else
    "$publish_directory/MaxMind.Db.NativeAot.App"
fi
