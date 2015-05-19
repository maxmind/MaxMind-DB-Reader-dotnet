To publish the to NuGet:

1. Update release notes.
2. Bump AssemblyVersion and AssemblyFileVersion.
3. Build solution.
4. From the MaxMind.Db directory:
   1. nuget pack MaxMind.Db.csproj
   2. nuget push MaxMind.Db.<version>.nupkg
