To publish the to NuGet:

1. Update release notes.
2. Bump `AssemblyFileVersion` and `AssemblyInformationalVersion` if in use.
  * Do _not_ bump the `AssemblyVersion` unless there has been a breaking
    change. We previously did this incorrectly. See [this StackOverflow
    question](http://stackoverflow.com/questions/64602/what-are-differences-between-assemblyversion-assemblyfileversion-and-assemblyin)
    for more information.
  * The `AssemblyInformationalVersionAttribute` is used to specify Semantic
    Versions not supported by the `AssemblyFileVersion` field. See the
    [NuGet Versioning documentation](https://docs.nuget.org/create/versioning#creating-prerelease-packages)
    for more information.
  * Due to incorrectly increasing the `AssemblyVersion`, it is at 1.0.1.0
    rather than 1.0.0.0. Ignore the temptation to increase it unless there
    is a breaking changes, which should only happen at 2.0.0 (or a
    pre-release of it).
3. Build solution.
4. From the MaxMind.Db directory:
   1. nuget pack MaxMind.Db.csproj
   2. nuget push MaxMind.Db.<version>.nupkg
5. Create tag of the form "v<version>".
6. Update GitHub Release page for the release.
