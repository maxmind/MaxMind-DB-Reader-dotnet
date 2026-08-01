#!/usr/bin/env bash

set -euo pipefail

readonly native_aot_rid="${1:?usage: test-native-aot.sh <runtime-identifier>}"
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
readonly repository_root
readonly package_version="5.2.0-aot-ci"
readonly package_directory="$repository_root/artifacts/native-aot-packages"
readonly app_project="$repository_root/MaxMind.Db.NativeAot/App/MaxMind.Db.NativeAot.App.csproj"
readonly netstandard_models_project="$repository_root/MaxMind.Db.NetStandard.TestModels/MaxMind.Db.NetStandard.TestModels.csproj"
restore_directory="$(mktemp -d)"
readonly restore_directory

mkdir -p "$package_directory"

dotnet pack "$repository_root/MaxMind.Db/MaxMind.Db.csproj" \
    --configuration Release \
    --output "$package_directory" \
    -p:PackageVersion="$package_version"

dotnet build "$netstandard_models_project" \
    --configuration Release \
    -p:MaxMindDbPackageVersion="$package_version" \
    -p:RestoreAdditionalProjectSources="$package_directory" \
    -p:RestorePackagesPath="$restore_directory" \
    -p:RestoreNoCache=true

dotnet publish "$app_project" \
    --configuration Release \
    --runtime "$native_aot_rid" \
    -p:MaxMindDbPackageVersion="$package_version" \
    -p:RestoreAdditionalProjectSources="$package_directory" \
    -p:RestorePackagesPath="$restore_directory" \
    -p:RestoreNoCache=true

readonly publish_directory="$repository_root/MaxMind.Db.NativeAot/App/bin/Release/net8.0/$native_aot_rid/publish"
if [[ "$native_aot_rid" == win-* ]]; then
    "$publish_directory/MaxMind.Db.NativeAot.App.exe"
else
    "$publish_directory/MaxMind.Db.NativeAot.App"
fi
