# Restore & Build Determinism Fix

Date: 2026-03-29  
Repository: `lvanzijl/PoCompanion`  
Validated environment: `/home/runner/work/PoCompanion/PoCompanion` on Ubuntu 24.04 with .NET SDK `10.0.201`

## Summary

Restore and build determinism were completed with the smallest repository-wide fix:

- added `global.json` to pin the exact validated .NET SDK
- enabled NuGet lock-file generation for all projects via `Directory.Build.props`
- generated and committed `packages.lock.json` for the current solution graph
- validated normal restore, locked restore, build, and tests against the committed lock graph

No application behavior, package sources, or persistence architecture were changed.

## SDK Pinning

Pinned SDK version: `10.0.201`

Rationale:

- `10.0.201` is the exact SDK version that was already used successfully for the green local baseline
- pinning removes machine-dependent SDK selection
- `rollForward` is set to `disable` so restore/build do not silently move to a different SDK patch band

Implemented in:

- `/home/runner/work/PoCompanion/PoCompanion/global.json`

## Lock File Strategy

NuGet lock files are now enabled repository-wide through:

- `/home/runner/work/PoCompanion/PoCompanion/Directory.Build.props`

Configured properties:

- `RestorePackagesWithLockFile=true`
- `RestoreLockedMode=true` when `ContinuousIntegrationBuild=true`

Effect:

- every project using `PackageReference` now produces a committed `packages.lock.json`
- local restore can regenerate lock files intentionally when dependencies are changed
- CI-oriented restores can enforce the committed graph in locked mode

Locked restore can be validated explicitly with:

```bash
dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -m:1 --locked-mode -v minimal
```

CI-style locked restore is also enforced automatically when restore runs with:

```bash
-p:ContinuousIntegrationBuild=true
```

## Files Added/Changed

Configuration files added:

- `/home/runner/work/PoCompanion/PoCompanion/global.json`
- `/home/runner/work/PoCompanion/PoCompanion/Directory.Build.props`

Lock files added:

- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Api/packages.lock.json`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Client/packages.lock.json`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain/packages.lock.json`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/packages.lock.json`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Core/packages.lock.json`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Integrations.Tfs/packages.lock.json`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Shared/packages.lock.json`
- `/home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/packages.lock.json`

Files intentionally not changed:

- `/home/runner/work/PoCompanion/PoCompanion/nuget.config`  
  Existing source configuration was already minimal and deterministic.

## Validation

Commands run:

```bash
dotnet --version
dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -m:1 --force-evaluate -v minimal
dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-restore -m:1 --nologo
dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build -v minimal

dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -m:1 --force-evaluate -v minimal
dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.sln -m:1 --locked-mode -v minimal
dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-restore -m:1 --nologo
dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Tests.Unit/PoTool.Tests.Unit.csproj --configuration Release --no-build -v minimal
dotnet test /home/runner/work/PoCompanion/PoCompanion/PoTool.Core.Domain.Tests/PoTool.Core.Domain.Tests.csproj --configuration Release --no-build -v minimal
```

Results:

- active SDK: `10.0.201`
- normal restore: succeeded
- lock-file generation restore: succeeded
- locked restore: succeeded
- release build: succeeded with `0` warnings and `0` errors
- unit tests: passed (`1680/1680`)
- domain tests: passed (`1/1`)

## Developer Workflow Impact

Normal day-to-day restore/build:

```bash
dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.sln
dotnet build /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --configuration Release --no-restore
```

Deterministic validation restore:

```bash
dotnet restore /home/runner/work/PoCompanion/PoCompanion/PoTool.sln --locked-mode
```

When dependencies intentionally change:

1. update the relevant `PackageReference` entries
2. run a normal restore to regenerate the affected `packages.lock.json` files
3. review the lock-file diffs to confirm the dependency graph change is expected
4. rerun locked restore, build, and tests
5. commit the project file changes together with the updated lock files

Because `global.json` pins the SDK exactly, developers and CI runners must have `.NET SDK 10.0.201` installed to restore and build successfully.

## Final Status

Restore/build determinism is now **fully deterministic for the repository’s intended model**:

- SDK/toolchain selection is pinned by `global.json`
- transitive package resolution is pinned by committed `packages.lock.json`
- locked restore is validated and available for CI enforcement

Within those constraints, restore and build are no longer only partially deterministic.

## Security Summary

- Reviewed with automated code review: no review comments were raised
- CodeQL analysis did not run because the change is configuration/documentation plus generated lock files, not analyzable source-code changes
- No new package sources, secrets, or executable behavior were introduced
- No security vulnerabilities were identified in the scope of this determinism fix
