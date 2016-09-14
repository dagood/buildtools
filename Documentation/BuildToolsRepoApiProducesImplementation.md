# Implementing [Repo API](RepoCompose.md) 'produces' command in buildtools

`Produces` describes the packages and other artifacts produced by a repo.  This includes anything that a repo creates which it then exposes for another file repo to consume.  These are files such as .nupkg's, .msi's, .exe's, etc...

This document proposes how the Core repo's will implement the `Produces` API described in [Repo API](RepoCompose.md).

## Overview

Each repo produces different artifacts in different ways.  It is not our goal to tightly couple the produces concept with a repo's build process.  We do want to provide a mechanism for each repo, which uses BuildTools, to provide Produces API information.  The proposed method is that BuildTools gathers `Produces` artifacts via a well-known build output structure.

## Produces

Each repo should expose a ".\produces\", that BuildTools will scan to generate the `Produces API` output.  Artifacts will go into a "grouping" folder in the ".\produces\artifacts\" folder, and the generated `produces.json` file will be created at ".\produces\produces.json". A "grouping" folder is an os-specific folder (see the [Repo API documentation](RepoCompose.md)). `Produces` data will be available locally and uploaded to the dotnet/versions repo where it will be available for `Consumes`.  The important thing is not the specific folder name, but that there is a single location which contains all of (and only) the `produces` artifacts.  The location itself we can provide a default for and allow a repo override.

BuildTools scans the `Produces` output and uploads it to dotnet/versions.  If a user executes 'run produces' in a repo, it will grab the latest `produces` info from the dotnet/versions repo or from the local produces output with dotnet/versions as a fallback (TBD).

In our current official builds, we produce all packages / artifacts for all OS's and we can publish this information.  Updates to dotnet/versions repo version of produces.json should only come from official builds.  We should be able to support the dev scenario where a single platform / architecture is being built and providing this information for a `change/consumes`. In the dev scenario, we would generate a subset of the full produces.json, that contains only what was locally built (or what is available in the build `produces` drop).  There are two assumptions here:

1. There are no x-plat dependencies between repos (including x64 dependencies on x86 artifacts).
- All artifacts, in the produces drop, follow a standard naming convention which includes OS / architecture information when appropriate.  
 -  This is to prevent artifact collisions, but OS / architecture information is only necessary where contents are dependent on those metrics.  Artifacts which are agnostic or platform or architecture would not require those pieces of naming data (per standard convention).

## Example

### Typical build

- Ensure that your produces artifacts are pushed into a "grouping" folder in the build output folder
 - MSBuild property: ProducesOutputDir
 - Default value: $(BinDir)\produces\
 - The artifacts are expected to be in the grouping subfolder of `ProducesPublishFolder` (MSBuild property), which defaults to "$(ProducesOutputDir)artifacts/"
  - Example: "$(ProducesOutputDir)artifacts/os-all"
 - The generated json file is controlled by the `ProducesJsonFilename` (MSBuild property), which defaults to "$(ProducesOutputDir)produces.json"
- Wire your repo to call the "GenerateProduces" target as a post-build step
 - ie, for CoreFx, you could edit src\post.builds and set `<Target Name="Build" DependsOnTargets="GenerateProduces"/>`
 
When the build completes you will have a produces.json file in `$(BinDir)\produces`.

### Official build

- Same requirements as for a "Typical build" and...
- Set the auth token for pushing files to the dotnet/versions repo
 - MSBuild property: VersionsRepoAuthToken
 - Default value: (empty)

If an auth token is specified, then the generated produces.json will be committed to the dotnet/versions repo.

## Sample produces.json
``` json
{
  "os-all": {
    "nuget": [
      {
        "packages": {
          "Microsoft.DotNet.PlatformAbstractions": "1.0.1-beta-000933",
          "Microsoft.Extensions.DependencyModel": "1.0.1-beta-000933"
        },
        "symbols-packages": {
          "Microsoft.DotNet.PlatformAbstractions": "1.0.1-beta-000933",
          "Microsoft.Extensions.DependencyModel": "1.0.1-beta-000933"
        }
      }
    ]
  },
  "os-windows": {
    "files": [
      "dotnet-host-win-x64.1.0.2-beta-000933-00.msi",
      "dotnet-host-win-x64.1.0.2-beta-000933-00.wixpdb",
      "dotnet-hostfxr-win-x64.1.0.2-beta-000933-00.msi",
      "dotnet-hostfxr-win-x64.1.0.2-beta-000933-00.wixpdb",
      "dotnet-hostfxr-win-x64.1.0.2-beta-000933-00.zip",
      "dotnet-sharedframework-win-x64.1.1.0-beta-000933-00.msi",
      "dotnet-sharedframework-win-x64.1.1.0-beta-000933-00.wixpdb",
      "dotnet-sharedframework-win-x64.1.1.0-beta-000933-00.zip",
      "dotnet-win-x64.1.1.0-beta-000933-00.exe",
      "dotnet-win-x64.1.1.0-beta-000933-00.wixpdb",
      "dotnet-win-x64.1.1.0-beta-000933-00.zip"
    ]
  }
}
```
