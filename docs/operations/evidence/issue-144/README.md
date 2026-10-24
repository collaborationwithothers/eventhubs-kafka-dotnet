# Worker container launch path repair

Both worker projects now use `/app/` as their container working directory. The pinned SDK appends the DLL name directly, so the trailing slash produces the intended `/app/Parchwell.Verifier.dll` and `/app/Parchwell.LoadGen.dll` launch paths. This addresses the start-line worker startup failure reported in [issue #144](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/144).

## Source and scope

The SDK behavior is in [Microsoft.NET.Build.Containers.targets at v10.0.402, lines 211 to 215](https://github.com/dotnet/sdk/blob/v10.0.402/src/Containers/packaging/build/Microsoft.NET.Build.Containers.targets#L211-L215). The [retained source](raw/sdk-container-targets.xml) contains the command assignment. The pod evidence supplied in the interactive session had no command or argument override and reported exit code 155 for `/appParchwell.Verifier.dll`; private pod identifiers are not reproduced here.

Hari requested the repair, cold review and PR directly in the interactive session. There is no separate approved item plan. The implementation changes only `ContainerWorkingDirectory` in the two project files. The project files are protected, so merge belongs to Hari.

## Validation

The [raw manifest](raw-manifest.json) records commands, source state, byte sizes and SHA-256 values. Validation used SDK 10.0.402 and the current source tree based on `9dd43b575292ff562efad6040d0cdac301ffd8e4`, with the two project edits whose hashes the manifest records.

| Check | Evidence | Result |
|---|---|---|
| Locked solution restore | [restore](raw/dotnet-restore.txt) | Passed |
| Release solution build | [build](raw/dotnet-build.txt) | Passed, zero warnings and errors |
| SDK-generated verifier command | [before](raw/verifier-before-command.json), [fixed](raw/verifier-fixed-command.json) | `/appParchwell.Verifier.dll` becomes `/app/Parchwell.Verifier.dll` |
| SDK-generated load generator command | [before](raw/loadgen-before-command.json), [fixed](raw/loadgen-fixed-command.json) | `/appParchwell.LoadGen.dll` becomes `/app/Parchwell.LoadGen.dll` |
| Existing verifier image-smoke unit tests | [tests](raw/verifier-smoke-tests.txt) | Passed |
| Existing load generator deployment/admission tests, including image smoke | [tests](raw/loadgen-smoke-tests.txt) | Passed |
| Image publication and container smoke | [publication attempt](raw/verifier-before-publish.txt) | Blocked by proxy HTTP 403 retrieving the pinned base image; no image was produced and no container smoke pass is claimed |
| Documentation advisory scan | [scan](raw/check-docs.txt) | Exit 0; warnings retained in unchanged upstream captures under `issue-114/raw/review-10-fixes/` |
| Diff whitespace | [check](raw/diff-check.txt) | Passed |

The before command checks use the original `/app` property as an MSBuild override; the fixed checks read the project property without an override. They run the SDK's actual `ComputeContainerConfig` target. The unit tests exercise packaged native/scenario inputs and worker startup helpers; they do not replace the blocked container execution checks. Image publication and execution under the deployment security settings remain unverified in this environment.

The documentation warnings refer to absent upstream includes, relative links and punctuation in historical raw evidence. Those captured bytes remain unchanged. No live scenario or Azure resource operation was run.
