$ErrorActionPreference = 'Stop'
$DebugPreference = 'Continue'

$projectJsonFile=(Get-Item "MaxMind.Db/project.json").FullName
$matches = (Get-Content -Encoding UTF8 releasenotes.md) ` |
            Select-String '(\d+\.\d+\.\d+(?:-\w+)?) \((\d{4}-\d{2}-\d{2})\)' `

$version = $matches.Matches.Groups[1].Value
$date = $matches.Matches.Groups[2].Value

if((Get-Date -format 'yyyy-MM-dd')  -ne $date ) {
    Write-Error "$date is not today!"
    exit 1
}

$tag = "v$version"

if (& git status --porcelain) {
    Write-Error '. is not clean'
}

# Not using Powershell's built-in JSON support as that
# reformats the file.
(Get-Content -Encoding UTF8 $projectJsonFile) `
    -replace '(?<=version"\s*:\s*")[^"]+', $version ` |
  Out-File -Encoding UTF8 $projectJsonFile


& git diff

if ((Read-Host -Prompt 'Continue? (y/n)') -ne 'y') {
    Write-Error 'Aborting'
}

if (-Not(& git status --porcelain)) {
    & git add $projectJsonFile
    & git commit -m "Prepare for $version"
}

Push-Location MaxMind.Db

& dotnet restore
& dotnet build -c Release
& dotnet pack -c Release

Pop-Location

Push-Location MaxMind.Db.Test

& dotnet restore
& dotnet test -c Release

Pop-Location

if ((Read-Host -Prompt 'Should push? (y/n)') -ne 'y') {
    Write-Error 'Aborting'
}

& git push

Pop-Location
& git tag "$tag"
& git push
& git push --tags

& nuget push "MaxMind.MinFraud/bin/Release/MaxMind.Db.$version.nupkg"
