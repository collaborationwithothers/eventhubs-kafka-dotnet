# Worker container launch path repair receipt

Both worker projects now use `/app/` as their container working directory. The pinned SDK appends the DLL name directly, so the trailing slash produces `/app/Parchwell.Verifier.dll` and `/app/Parchwell.LoadGen.dll`. This repairs the startup failure reported in [issue #144](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/144).

The SDK behavior is in [Microsoft.NET.Build.Containers.targets at v10.0.402, lines 211 to 215](https://github.com/dotnet/sdk/blob/v10.0.402/src/Containers/packaging/build/Microsoft.NET.Build.Containers.targets#L211-L215). The user-supplied pod had no command override and reported exit code 155 for `/appParchwell.Verifier.dll`. Private pod identifiers are omitted.

## Scope and authority

Hari requested the repair and PR directly in the interactive session. His [2026-10-24 exception](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/144#issuecomment-6381034951) adds the receipt cleanup and matching AGENTS.md/CLAUDE.md changes to this PR. The [#85 decision of 2026-10-23](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6358395533) and its [correction](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/85#issuecomment-6358660171) require no committed program output and one authored receipt per PR.

This PR removes only its own generated evidence and manifest. Existing evidence directories and `.gitattributes` remain unchanged. The protected project and governance files require Hari's merge.

## Worker validation

Source: `9dd43b575292ff562efad6040d0cdac301ffd8e4` with the two project edits subsequently committed in `5b348e9338ba1eaf6a56b11c4e061cb3fc33f97d`. The worker project contents are unchanged by the receipt cleanup. Commands used SDK 10.0.402 at `/tmp/a01-dotnet/dotnet`; results below are authored observations of the session output.

| Command | Result |
|---|---|
| `timeout 600 /tmp/a01-dotnet/dotnet restore Parchwell.slnx --locked-mode` | Exit 0 |
| `timeout 600 /tmp/a01-dotnet/dotnet build Parchwell.slnx --configuration Release --no-restore` | Exit 0; zero warnings and errors |
| `/tmp/a01-dotnet/dotnet msbuild src/Parchwell.Verifier/Parchwell.Verifier.csproj -t:ComputeContainerConfig -p:RuntimeIdentifier=linux-x64 -p:SelfContained=false -getItem:ContainerAppCommand -getProperty:ContainerWorkingDirectory -p:ContainerWorkingDirectory=/app` | Exit 0; command is `dotnet /appParchwell.Verifier.dll` |
| `/tmp/a01-dotnet/dotnet msbuild src/Parchwell.Verifier/Parchwell.Verifier.csproj -t:ComputeContainerConfig -p:RuntimeIdentifier=linux-x64 -p:SelfContained=false -getItem:ContainerAppCommand -getProperty:ContainerWorkingDirectory` | Exit 0; command is `dotnet /app/Parchwell.Verifier.dll` |
| `/tmp/a01-dotnet/dotnet msbuild src/Parchwell.LoadGen/Parchwell.LoadGen.csproj -t:ComputeContainerConfig -p:RuntimeIdentifier=linux-x64 -p:SelfContained=false -getItem:ContainerAppCommand -getProperty:ContainerWorkingDirectory -p:ContainerWorkingDirectory=/app` | Exit 0; command is `dotnet /appParchwell.LoadGen.dll` |
| `/tmp/a01-dotnet/dotnet msbuild src/Parchwell.LoadGen/Parchwell.LoadGen.csproj -t:ComputeContainerConfig -p:RuntimeIdentifier=linux-x64 -p:SelfContained=false -getItem:ContainerAppCommand -getProperty:ContainerWorkingDirectory` | Exit 0; command is `dotnet /app/Parchwell.LoadGen.dll` |
| `timeout 180 /tmp/a01-dotnet/dotnet test --project tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj --configuration Release --no-build --minimum-expected-tests 1 -- --filter-class '*ImageSmokeTests'` | Exit 0; 3 passed, zero failed or skipped |
| `timeout 180 /tmp/a01-dotnet/dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj --configuration Release --no-build --minimum-expected-tests 1 -- --filter-class '*DeploymentAdmissionTests'` | Exit 0; 23 passed, zero failed or skipped |
| `/tmp/a01-dotnet/dotnet publish src/Parchwell.Verifier/Parchwell.Verifier.csproj --configuration Release -r linux-x64 --self-contained false -t:PublishContainer -p:RestoreLockedMode=true -p:NuGetLockFilePath=packages.linux-x64.lock.json -p:ContainerImageTag=issue144-before -p:ContainerArchiveOutputPath=/tmp/a01-images-144/verifier-before.tar.gz -p:ContainerWorkingDirectory=/app` | Exit 1; proxy HTTP 403 retrieving the pinned base image. No image produced; container smoke execution remains unverified |

The before checks override the working directory with the original `/app`; the fixed checks read the project property. They invoke the SDK's actual `ComputeContainerConfig` target. The selected tests exercise native/scenario packaging and startup helpers; they do not establish successful container execution. No live scenario or Azure resource operation was run.

## Receipt and governance validation

Source: `5b348e9338ba1eaf6a56b11c4e061cb3fc33f97d` with this PR's staged receipt/governance cleanup. Program output stays in the session workspace.

| Command | Result |
|---|---|
| `npm run check:docs` | Exit 0; 36 advisory warnings in unchanged historical captures; no issues in the changed Markdown files |
| `git diff --cached --check` | Exit 0 |

The documentation warnings refer to absent upstream includes, relative links and punctuation in unchanged captures under `issue-114/raw/review-10-fixes/`. Those historical files remain unchanged. Cold-review results are recorded in the PR's self-review section.
