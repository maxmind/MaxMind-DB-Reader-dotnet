#!/bin/bash

set -e
set -u

shopt -s extglob

PROJECT_JSON="MaxMind.Db/project.json"

VERSION=$(perl -MFile::Slurp::Tiny=read_file -MDateTime <<EOF
use v5.16;
my \$today = DateTime->now->ymd;
my \$log = read_file(q{releasenotes.md});
\$log =~ /\n## (\d+\.\d+\.\d+(?:-\w+)?) \((\d{4}-\d{2}-\d{2})\) ##\n/;
die "Release time is not today! Release: \$2 Today: \$today"
    unless \$today eq \$2;
say \$1;
EOF
)

TAG="v$VERSION"

if [ -n "$(git status --porcelain)" ]; then
    echo ". is not clean." >&2
    exit 1
fi

jq ".version=\"$VERSION\"" "$PROJECT_JSON"| sponge "$PROJECT_JSON"

git diff

read -e -p "Continue (and commit above)? " SHOULD_COMMIT

if [ "$SHOULD_COMMIT" != "y" ]; then
    echo "Aborting"
    exit 1
fi

if [ -n "$(git status --porcelain)" ]; then
    git add "$PROJECT_JSON"
    git commit -m "Prepare for $VERSION"
fi

pushd MaxMind.Db

dotnet restore
dotnet build -c Release

# We do not release from Linux as signing doesn't work properly. See:
# https://github.com/dotnet/cli/issues/720 and
#
# dotnet pack -c Release

popd

pushd MaxMind.Db.Test

dotnet restore

# netcoreapp1.0 only due to https://github.com/dotnet/cli/issues/3073
dotnet test -c Release -f netcoreapp1.0

popd

read -e -p "Push to origin? " SHOULD_PUSH

if [ "$SHOULD_PUSH" != "y" ]; then
    echo "Aborting"
    exit 1
fi

git tag "$TAG"
git push
git push --tags

# nuget push "MaxMind.Db/bin/Release/MaxMind.Db.$VERSION.nupkg"

echo "Currently, signing does not work properly from Linux. Please pack and release from Windows."
