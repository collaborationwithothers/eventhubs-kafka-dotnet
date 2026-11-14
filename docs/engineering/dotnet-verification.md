# .NET foundation verification

[Issue #47's approved implementation plan][approved-plan] established the build foundation and executable architecture checks. The Kafka library now has [baseline mapping tests](../operations/kafka-baseline.md), and its test project also runs [local Docker characterization](../operations/phantom-spike/README.md). A successful build alone establishes no scenario behavior.

## Prerequisites and commands

The supported .NET verification environment is Linux with Bash and .NET SDK **10.0.402** on `PATH`. Install prerequisites separately. `global.json` disallows SDK roll-forward and prereleases, and selects Microsoft Testing Platform. The verification script checks the selected SDK and never installs or upgrades tools.

From the repository root:

```bash
./scripts/verify-dotnet.sh
```

The Bash script invokes .NET directly. Its normal restore and build are:

```bash
dotnet restore Parchwell.slnx --locked-mode
dotnet build Parchwell.slnx --configuration Release --no-restore
```

The required architecture test invocation uses a fresh results directory:

```bash
dotnet test \
  --project tests/Parchwell.ArchitectureTests/Parchwell.ArchitectureTests.csproj \
  --configuration Release \
  --no-build \
  --minimum-expected-tests 1 \
  --results-directory "$verification_results/Parchwell.ArchitectureTests" \
  -- \
  --report-xunit-xml \
  --report-xunit-xml-filename results.xml
```

The script sets `verification_results`, creates the assembly-specific results directory, and owns report validation. Running only the commands above omits its other checks and controls. The [pinned MTP command documentation][mtp-command] describes the runner options. `--minimum-expected-tests` counts skipped tests too; a separate report check therefore requires passing tests and rejects skips.

Documentation verification has separate Node >=24 and npm prerequisites, declared in `package.json`:

```bash
npm ci --ignore-scripts
npm run test:docs
npm run check:docs
```

Those commands check documentation. They are not the .NET build entry point. Documentation warnings remain advisory and must be fixed or explained before cold review under [AGENTS.md](../../AGENTS.md#before-opening-a-pull-request).

## Tiers, partial runs and failure reporting

`./scripts/verify-dotnet.sh` with no options is the full gate: every test project and every test. Hosted CI runs each test project, or each shard of a sharded test project, in its own job with `./scripts/verify-dotnet.sh --project <name>`, adding `--shard <shard>` for a shard, so a single hosted job is a partial run; the `.NET verification` aggregate job requires all of them ([Hosted CI and completion](#hosted-ci-and-completion)). These options give partial runs, for quicker feedback or, with `--tier sandbox`, for the pre-PR local gate, and never replace the full gate:

```bash
./scripts/verify-dotnet.sh --tier fast
./scripts/verify-dotnet.sh --tier sandbox
./scripts/verify-dotnet.sh --project Parchwell.Verifier.Tests --project Parchwell.Kafka.Tests
./scripts/verify-dotnet.sh --project Parchwell.Verifier.Tests --shard b10-verify-a
```

Setting `PARCHWELL_BUILD_MAX_CPU` without options leaves the run a full gate:

```bash
PARCHWELL_BUILD_MAX_CPU=1 ./scripts/verify-dotnet.sh
```

`--tier fast` passes `--filter-not-trait Tier=Docker Tier=Slow Tier=CGroupV2` to every test project. The option names were read from `dotnet test --help` on SDK 10.0.402 on 2026-11-12, and one run showed the fast tier removing exactly the marked tests. In the recorded fast-tier run (`items/435/receipt.md`) every report passed the check that rejects skips, and the number of removed tests equalled the number of marked tests. From that I infer that the runner leaves filtered-out tests out of the report. No cited source states it. The fast tier also leaves out the spike-evidence publication, because the Docker spike test produces that evidence. `--tier sandbox` passes `--filter-not-trait Tier=CGroupV2` to every test project, so it leaves the Docker and Slow tests in and publishes the spike evidence, as `--tier all` does. A `sandbox` run prints the partial line and is cited as a partial run. It is the pre-PR local gate defined in [Verification gates](../agents/pull-requests.md#verification-gates), which is where the rule lives. `--project` runs only the named test project; give the option more than once to run several. A run with `--tier fast`, `--tier sandbox` or `--project` prints `Partial verification passed` and states that it is not the full gate. Cite a partial run as a partial run, never as the gate.

`--shard <name>` runs one shard of the single `--project` it goes with. A shard is a set of test classes that `scripts/dotnet-test-shards.tsv` names; the file's header comments and its tab-separated lines (project, shard, class) define the format. A line is empty, a comment that starts with `#`, or has exactly three tab-separated fields. The project matches `Parchwell.` followed by letters, digits and dots. The shard is lowercase letters and digits in groups joined by single hyphens, and is not `rest`. The class starts with the project name and a dot and has no space. A project and class pair appears once, whatever the shard. Both the script and the aggregate checker reject a file that breaks these rules. A project that the file names is sharded. Its shards are the named shards in order of first appearance, then the implicit shard `rest`. A named shard passes `--filter-class` with its classes; `rest` passes `--filter-not-class` with every class the file lists for that project. These go after the tier arguments. Several values after one switch are allowed. The xUnit runner's own help, from the in-process runner v4.0.2 that the pinned `xunit.v3.mtp-v2` 4.0.2 provides (`dotnet <test assembly>.dll --help`, read 2026-11-13), states that filters of different types combine as AND and that `-class` given more than once acts as OR. The [xUnit Microsoft Testing Platform page](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform) names `--filter-class` and `--filter-not-class` and says that several filters of one kind can follow a single switch, but does not say how filters of different kinds combine. `PARCHWELL_TEST_SHARDS_FILE` points the script at another shard file. The script rejects a bad request with exit status 2 before it calls `dotnet`: a `--shard` without exactly one `--project`, a repeated `--shard`, an unreadable or malformed shard file, a project the file does not name, or a shard the project does not have. The `scripts/test-verify-dotnet.sh` cases check each rejection.

After a project's tests and report stages pass, the stage `<name> test list` runs `dotnet run --project <project> --configuration Release --no-build --no-launch-profile -- -list methods/json -noColor` and writes its standard output to `methods.json` in the project's results directory. The runner help cited above describes `-list methods` as listing the class and method name of every method that is a test, `/json` as the JSON form, and `-noColor` as an option; without `-noColor` the output ended with an ANSI reset sequence that broke the JSON in the planning session's run. That the strings are `<type>.<method>` and equal the method set a full run reports is observed, not stated by a source: the six test projects matched in the planning run recorded in `items/452/plan-test-shards.md` section 7, and one Verifier shard matched in `items/452/receipt.md` (A8). The aggregate job checks the equality on every run. The aggregate job compares it with the report ([Hosted CI and completion](#hosted-ci-and-completion)). A failing test list stage fails the project like a failing tests stage.

Three values of the xUnit trait `Tier` mark tests that the fast tier leaves out:

| Trait | Mark a test with it when |
|---|---|
| `Docker` | It needs a Docker daemon, directly or through Testcontainers. |
| `Slow` | One run of the test takes more than 30 s on a 4-core machine. The 30 s threshold is a policy choice, not a measurement. The three current marks come from the baseline run of 2026-11-12 (142.2 s, 121.1 s and 41.2 s, listed in `items/435/receipt.md`). |
| `CGroupV2` | It needs the host's cgroup v2 memory files, such as `memory.events.local`, which the abrupt-exit capture reads. Managed cloud sandboxes may not provide them. |

Put the trait on the class when every test in it qualifies, and on the method otherwise. A test that needs Docker but carries no trait still runs in the full gate and in the fast tier. On a machine without Docker it fails in the fast tier, which is visible and safe. It does not disappear.

In a Claude Code cloud session, the `SessionStart` hook `.claude/hooks/start-dockerd.sh` starts the Docker daemon in the background, so the `Docker` tests can run there. It acts only when `CLAUDE_CODE_REMOTE` is `true`, the session runs as root, `dockerd` is installed and no `dockerd` process is running. It does not wait for the daemon. In the run recorded in [issue #445](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445), with `dockerd` started in the background by hand rather than by this hook, the daemon was ready within 40 s. Run `docker info` before the `Docker` tests; it fails until the daemon is ready. The daemon's output goes to `parchwell-dockerd.log` in `$TMPDIR`, or in `/tmp` when `TMPDIR` is unset. The sandbox of that run lacked the cgroup v2 file `memory.events.local`, so `B10AbruptEndToEndTests` failed there with `b10_abrupt_cgroup_unavailable`. `.claude/hooks/tests/start-dockerd.test.sh` checks the hook against stub commands and starts no real daemon.

The script stops at once only for stages that every later stage depends on: restore, build, the Kafka XML inventory check, project evaluation and the scaffold inventory. After those, a failing test project, the mutation-only Verifier test build or the warning control is recorded and the run continues, so one run reports every failing project. The script then prints a stage timing table, lists each failed stage, and exits with the exit status of the first failure. Each stage line also gives the stage's UTC start and end. The script writes the same table to `stage-timing.tsv` in its results directory, one line per stage with four tab-separated fields: label, whole seconds, UTC start and UTC end.

The mutation-only Verifier test build compiles `tests/Parchwell.Verifier.Tests` with `-p:ParchwellVerifierMutationOnly=true`, the configuration the C5 mutation run uses, and fails on any build error. It runs when the Verifier test project is selected and the run has no shard or the shard `rest`, so each pull request run builds it once. It builds into its own artifacts directory, so it cannot replace the assemblies that the test stages run. Issue #404 added it after a file that the mutation-only build had removed broke that build unseen.

On every exit after it creates its results directory, the script also writes `summary.json` there. Writing it never changes the exit status. It records the run's UTC start and end, the source commit and whether the working tree had changes, the tier, the selected projects and shard, the processor count from `nproc`, total memory from `/proc/meminfo`, the values of `DOTNET_PROCESSOR_COUNT` and `PARCHWELL_BUILD_MAX_CPU` as set, each stage with its exit status, the failed stages, the exit status, the outcome, and the 30 slowest tests of each test project's report. A test name there is xUnit's escaped display name, as the report holds it.

| Key | Type |
|---|---|
| `schema_version` | number `1` |
| `started_utc`, `ended_utc` | string, UTC time in the form `YYYY-MM-DDTHH:MM:SSZ`; start is taken when the results directory is created, end when the summary is written at exit |
| `source_sha` | string from `git rev-parse HEAD` in the repository root, or `null` when that command fails |
| `source_dirty` | `true` when `git status --porcelain` prints anything, `false` when it prints nothing, `null` when it fails |
| `sdk_version` | string, the checked SDK version |
| `tier` | `"all"`, `"fast"` or `"sandbox"` |
| `projects` | `null` without `--project`; otherwise array of the given names in the given order |
| `shard` | `null` without `--shard`; otherwise the `--shard` value as a string |
| `nproc` | number, output of `nproc` |
| `mem_total_kib` | number, the `MemTotal` value in kB from `/proc/meminfo`, or `null` when that line is absent or the file cannot be read |
| `dotnet_processor_count` | string, the `DOTNET_PROCESSOR_COUNT` value exactly as set, or `null` when unset |
| `parchwell_build_max_cpu` | string, the `PARCHWELL_BUILD_MAX_CPU` value exactly as set, or `null` when unset or empty |
| `stages` | array in run order, one object per line of `stage-timing.tsv`: `{"label": string, "elapsed_s": number, "start_utc": string, "end_utc": string, "exit_code": number}` |
| `failed_stages` | array of `{"label": string, "exit_code": number}` in order: each failed test project, the mutation-only Verifier test build or the warning control, labelled with the test project name, `Mutation-only Verifier test build` or `Warning control`, or, for a stage that stops the run, one entry: that stage's label, or `Project evaluation` for project evaluation; empty array otherwise |
| `exit_code` | number, the script's exit status |
| `outcome` | `"full_gate_passed"` when the full-gate line printed, `"partial_passed"` when the partial line printed, `"failed"` otherwise |
| `slowest_tests` | object keyed by test project name, one key for each project whose `<results directory>/<project>/results.xml` exists at exit: array of at most 30 `{"name": string, "time_s": string}`; `{}` when no report exists |

`PARCHWELL_BUILD_MAX_CPU` adds `-maxcpucount:<n>` to restore, build, project evaluation and the warning control. It leaves the test processes alone. Do not set `DOTNET_PROCESSOR_COUNT` for the whole script: that changes the processor count that tests observe, and a Codex session recorded eight failures after doing so, with the cause not proved to be that setting (`items/404/b10-warm-leader-receipt.md`, V11). Whether the cap stops the MSBuild worker exits recorded there (V2) is not verified; it was not reproduced.

`./scripts/test-verify-dotnet.sh` checks the option handling, fail-late behaviour, stage timing and `summary.json` of the script against a stub `dotnet`. It needs no SDK and no Docker, but needs `jq` to read `summary.json`. It ran in 1.5 s in the one measured run (`items/435/receipt.md`), before the stage timing and summary checks were added.

## Enforced gates

`Directory.Build.props` sets `net10.0`, nullable references, warnings as errors, `latest-recommended` analysis, enabled .NET analyzers and analyzer execution during build. The script collects evaluated MSBuild configuration, declared project references and compile items in Release. The architecture suite reads those evaluated manifests, rather than treating the presence of root declarations as proof that every project inherits them.

The architecture suite requires the documented source/test inventory in `Parchwell.slnx`, the effective correctness settings, and the declared dependency direction. `Parchwell.Kafka` cannot reference another source project, and the services cannot reference one another. Declared references are checked even when no types from a dependency are used.

Compiled rules use ArchUnitNET to check the same dependency direction, isolation between feature slices within each service, and calls to the exact `DateTime.Now` and `DateTime.UtcNow` getters. The rule selects `get_Now()` and `get_UtcNow()` using the [pinned loader's method naming][getter-naming]. Other `DateTime` use remains allowed. Missing expected source assemblies fail verification. The Kafka library now contains the [baseline configuration mapping](../operations/kafka-baseline.md), which has paired behavior tests. Service projects remain scaffolds, so their compiled slice and clock checks still have limited coverage.

Service namespaces follow their folders. For example, `src/Parchwell.LoadGen/Publish/Handler.cs` declares `Parchwell.LoadGen.Publish`; deeper folders add namespace segments. Folder names are single, unescaped C# identifiers. The first folder below the service directory defines the feature slice, with `Common/` reserved for shared behavior under the engineering standards. The architecture suite parses every evaluated service `Compile` item with Roslyn from the pinned SDK and requires each class, struct, interface, enum, record and delegate declaration to match that file's folder namespace. It combines nested namespace declarations, handles file-scoped namespaces and checks each partial declaration. Types without namespaces and source syntax errors fail. A file outside its project requires explicit relative `Link` metadata mapping it inside that project's logical folders; unmapped external files and links escaping the project fail.

The parser uses each project's effective Release `LangVersion` and `DefineConstants`, including SDK symbols generated by [the pinned `AddImplicitDefineConstants` target][sdk-define-constants]. Inactive conditional code is excluded by Roslyn's [parse options][roslyn-parse-options] and [syntax-tree parser][roslyn-parser]. The architecture project references `Microsoft.CodeAnalysis` and `Microsoft.CodeAnalysis.CSharp` from `$(MSBuildToolsPath)/Roslyn/bincore`; these compiler assemblies are supplied by SDK 10.0.402, with versions and source commits recorded in its [dependency manifest][sdk-roslyn-dependencies]. No NuGet package or extra project is added. The existing compiled slice rule then checks dependencies between the mapped namespaces and also rejects service types outside their service namespace.

`.editorconfig` also makes SDK analyzer `IDE0130` an error for the three services, which enable `EnforceCodeStyleInBuild` and use their project name as `RootNamespace`. Effective-setting tests check those values and the compiler-visible `RootNamespace` and `ProjectDir` metadata supplied by the [SDK targets][sdk-namespace-analyzer]. The [IDE0130 rule][namespace-rule] supplies an early build diagnostic for ordinary declarations; the syntax gate covers the [analyzer's exclusions][namespace-analyzer-source] for nested namespaces, multiple partial declarations and external linked files. Namespace and cross-slice negative controls remain deferred, so successful scaffold verification does not establish their rejection paths.

Fresh machine-readable xUnit reports must identify the expected test assembly, contain a positive passed-test count, and have no failed, skipped or not-run tests or runner errors. Summary counts must agree with the individual results. `scripts/dotnet-verification.proj` parses XML and rejects missing or malformed reports. The [pinned xUnit writer][xml-fields] supplies the checked assembly result fields.

A paired unit-test project is reported as an empty scaffold only when its evaluated `Compile` item list is empty. Any compile item makes that project execute under the same result requirements, including explicitly linked files under `bin/` or `obj/`. The exemption does not permanently exclude that project. The Kafka test project executes its mapping tests and Docker characterization; the additional [mapping coverage pass](../operations/kafka-baseline.md#verification) supplements full verification. After the solution build, the entry point also checks generated Kafka setting XML against the catalogue inventory IDs. The [CI mapping coverage steps](../operations/kafka-baseline.md#ci-mapping-coverage) run in the Kafka test project's job after its verification and reject missing coverage or uncovered mapping lines. There are no dummy passing tests.

## Approved controls

The foundation negative controls approved in #47 are:

| Control | Valid counterpart | Required rejection |
|---|---|---|
| Declared project reference | An isolated fixture with an allowed reference graph | The real declared-reference validator rejects an unused service-to-service reference and identifies the forbidden edge |
| Warning as error | The fixture without the intentional warning | Compilation rejects `CS1030` with the `PARCHWELL_WARNING_CONTROL` marker as an error |

The architecture suite evaluates the valid and forbidden-reference fixture projects and runs both graphs through the real reference validator. The Bash script builds the warning fixture's valid and invalid counterparts. Fixtures run in isolated temporary locations and do not edit production projects. A failing fixture counts only when its intended rule or diagnostic is verified. Missing prerequisites, restore errors and unrelated build failures do not satisfy a control.

Cross-slice references, direct clock calls, separate nullable/analyzer violations, failing tests and zero-test discovery have no negative controls in this PR. Their underlying gates remain required, but this PR does not claim their rejection paths were exercised. Additional controls wait until a slice needs them, as Hari's amendment requires.

## Diagnostics and dependency changes

The script prints concise gate summaries and retains detailed logs and fresh results in `${TMPDIR:-/tmp}/parchwell-dotnet-verification.XXXXXXXX`, with a generated suffix. It prints the actual directory path on exit. The architecture report is `Parchwell.ArchitectureTests/results.xml` within that directory. Build artifacts stay in each project's ignored `bin/` and `obj/` directories. Unexpected subprocess failures preserve their exit codes; accepted negative-control failures still require the intended cause. Keep the printed directory when investigating a failure.

Direct package versions are centralized in `Directory.Packages.props`, and per-project `packages.lock.json` files record the resolved graphs. Normal verification uses locked restore and cannot silently refresh those graphs. Change package versions and regenerate affected lock files deliberately in a reviewed dependency-change PR.

| Direct package | Pin | Use |
|---|---|---|
| `xunit.v3.mtp-v2` | 4.0.2 | Every test project |
| `TngTech.ArchUnitNET` | 0.13.4 | Architecture tests only |
| `Confluent.Kafka` | 2.16.0 | Kafka library and Kafka tests |
| `Testcontainers` | 4.16.0 | Kafka Docker characterization |
| `Microsoft.Extensions.Options` | 10.0.13 | Kafka library options and startup validation |
| `Microsoft.Extensions.DependencyInjection` | 10.0.13 | Kafka in-process DI tests |

The exact official [xUnit manifest][xunit-manifest], [ArchUnitNET manifest][archunit-manifest], [Kafka manifest][kafka-manifest], [Testcontainers manifest][containers-manifest], [Options manifest][options-manifest] and [DI manifest][di-manifest] are the sources for these pins. The Kafka additions follow the [spike plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/52#issuecomment-6172812371) and [baseline plan](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/54#issuecomment-6174869137). There is no separate VSTest adapter/Test SDK or architecture xUnit integration package.

## Worker image smoke

`./scripts/verify-worker-images.sh` publishes the LoadGen and Verifier container images and starts each one, so a base image that lacks a framework the app needs fails before any cloud run. The base image rule is in [.NET engineering standards](dotnet.md#kubernetes-workers). The script needs .NET SDK 10.0.402 and a running Docker daemon, and takes no arguments.

For each worker it runs the repository's locked linux-x64 container publish to a local archive and loads the archive into Docker. It then starts the image with `--image-smoke`, no network, a read-only root file system, user `1654:1654`, all capabilities dropped and `no-new-privileges`. It fails when the SDK version differs, Docker is unreachable, a publish or load fails, a container exits non-zero, or a container prints to stdout anything other than `{"native_loaded":true,"scenario_files":3,"uid_expected":1654}`. It stops at the first failing worker. It removes its containers and images on exit and prints its log directory.

Regression example ([#179](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/179)): with the `mcr.microsoft.com/dotnet/runtime` base image that `main` used at `8afef30`, the LoadGen container exits 150 because the `Microsoft.AspNetCore.App` framework is missing, and the script fails. With the `aspnet` base image both containers exit 0 and the script passes. The receipt in `docs/operations/evidence/issue-179/README.md` records the failing run and the passing hosted `.NET verification` run on the pull request's code head, the last commit before the receipt-only commit that completes the receipt.

## Hosted CI and completion

The [verification workflow][workflow] runs on standard GitHub-hosted `ubuntu-24.04`, for pull requests, pushes to `main` and manual dispatch. Its sole repository permission is `contents: read`; SDK selection comes from `global.json`. Besides the lifecycle and Terraform verification jobs, it has three kinds of .NET job:

- One `.NET tests (<unit>)` job per unit runs `./scripts/verify-dotnet.sh --project <name>`, with `--shard <shard>` when the unit is a shard. A unit is a whole test project, or one shard of a sharded project, written `<project>--<shard>`. The job for the Kafka test project also installs standalone `dotnet-coverage` 18.12.0 under `$RUNNER_TEMP/parchwell-coverage` and invokes `./scripts/verify-kafka-coverage.sh` with that collector path. Each job uploads an artifact named `dotnet-verification-<unit>-attempt-<n>` that holds whichever of `summary.json`, `stage-timing.tsv`, `unit-projects.txt`, `results.xml` and `methods.json` the run produced, and, only when every earlier step passed, an empty `leg-passed` file. A job that fails after `verify-dotnet.sh` has created its results directory may leave some of these out, and the aggregate job reports each missing file. A job that fails before that point, such as a failed checkout or SDK setup, uploads no artifact, and the aggregate job reports the unit as missing (`missing-project` for a whole project, `missing-shard` for a shard).
- `Worker image smoke` runs the [worker image smoke](#worker-image-smoke) in its own job.
- `.NET verification`, the required check, is an aggregate job. It runs `scripts/check-dotnet-matrix.py`, which fails unless both other jobs succeeded and every project of the scaffold inventory plus `Parchwell.ArchitectureTests` has a passing summary, a passed report stage and the passed marker. It also reads `scripts/dotnet-test-shards.tsv` and requires one artifact for every expected unit. It requires each listed test method of a project to run in exactly one job of that project, and each class that a shard names to run only in that shard's job. It compares each job's `methods.json` with its `results.xml`, so a filter that drops or repeats a test fails the check.

The local full gate stays `./scripts/verify-dotnet.sh` with no options. The [Ubuntu 24.04 runner image readme][runner-readme], read at a fixed commit, lists Docker Server 28.0.4 for image version 20261023.334.3; `runs-on: ubuntu-24.04` does not pin that image version. The collector pin and local equivalent are documented in the [Kafka baseline verification guide](../operations/kafka-baseline.md#verification). The workflow has no Azure identity, private-network runner or live tests.

The approved immutable pins are [checkout v7.0.1][checkout-release] at `3d3c42e5aac5ba805825da76410c181273ba90b1` and [setup-dotnet v6.0.0][setup-release] at `a98b56852c35b8e3190ac28c8c2271da59106c68`, [upload-artifact v4.6.2][upload-release] at `ea165f8d65b6e75b540449e92b4886f43607fa02` and [download-artifact v4.3.0][download-release] at `d3f86a106a0bac45b974a628896c90dbdf5c8093`. The pinned [setup-dotnet README][setup-readme] documents the `global-json-file` input. This describes implemented workflow configuration, not proof of a successful hosted run.

Hari explicitly authorized this workflow change in the approved issue plan. If GitHub rejects the workflow push for missing credential permission, hand off the exact YAML and rejection; do not change authentication to work around it.

Issue #47 stays open until hosted verification passes on the reviewed PR head and Hari makes the `.NET verification` check required in branch protection. Record the tested commit and SDK alongside local results, and separately record the hosted run's head SHA. Hari retains the merge decision. Existing documentation CI stays advisory.

[approved-plan]: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/47#issuecomment-6170342354
[mtp-command]: https://github.com/dotnet/docs/blob/b6c9cb41353de8cd352c499be9e49ea6b4825bae/docs/core/tools/dotnet-test-mtp.md
[xunit-manifest]: https://api.nuget.org/v3-flatcontainer/xunit.v3.mtp-v2/4.0.2/xunit.v3.mtp-v2.nuspec
[archunit-manifest]: https://api.nuget.org/v3-flatcontainer/tngtech.archunitnet/0.13.4/tngtech.archunitnet.nuspec
[kafka-manifest]: https://api.nuget.org/v3-flatcontainer/confluent.kafka/2.16.0/confluent.kafka.nuspec
[containers-manifest]: https://api.nuget.org/v3-flatcontainer/testcontainers/4.16.0/testcontainers.nuspec
[options-manifest]: https://api.nuget.org/v3-flatcontainer/microsoft.extensions.options/10.0.13/microsoft.extensions.options.nuspec
[di-manifest]: https://api.nuget.org/v3-flatcontainer/microsoft.extensions.dependencyinjection/10.0.13/microsoft.extensions.dependencyinjection.nuspec
[checkout-release]: https://github.com/actions/checkout/releases/tag/v7.0.1
[setup-release]: https://github.com/actions/setup-dotnet/releases/tag/v6.0.0
[upload-release]: https://github.com/actions/upload-artifact/releases/tag/v4.6.2
[download-release]: https://github.com/actions/download-artifact/releases/tag/v4.3.0
[setup-readme]: https://github.com/actions/setup-dotnet/blob/a98b56852c35b8e3190ac28c8c2271da59106c68/README.md
[runner-readme]: https://github.com/actions/runner-images/blob/2deeef005a7e79479c137ef48d6f07f2cca37880/images/ubuntu/Ubuntu2404-Readme.md
[workflow]: ../../.github/workflows/dotnet-verification.yml
[getter-naming]: https://github.com/TNG/ArchUnitNET/blob/1ab5943d761d48f86b42f45ef047130dc9aff1c6/ArchUnitNET/Loader/MonoCecilMemberExtensions.cs#L47-L80
[xml-fields]: https://github.com/xunit/xunit/blob/0c7aa4991fa340689155d52043ce88f52f2f7421/src/xunit.v3.runner.common/Results/Builtin/XmlV2ResultWriterMessageHandler.cs#L292-L308
[namespace-rule]: https://github.com/dotnet/docs/blob/b6c9cb41353de8cd352c499be9e49ea6b4825bae/docs/fundamentals/code-analysis/style-rules/ide0130.md
[sdk-namespace-analyzer]: https://github.com/dotnet/sdk/blob/v10.0.402/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.Analyzers.targets#L137-L164
[namespace-analyzer-source]: https://github.com/dotnet/dotnet/blob/3073618cc1ab03586872e1cf3035afd377d66df6/src/roslyn/src/Analyzers/Core/Analyzers/MatchFolderAndNamespace/AbstractMatchFolderAndNamespaceDiagnosticAnalyzer.cs
[sdk-define-constants]: https://github.com/dotnet/sdk/blob/v10.0.402/src/Tasks/Microsoft.NET.Build.Tasks/targets/Microsoft.NET.Sdk.BeforeCommon.targets#L290-L297
[sdk-roslyn-dependencies]: https://github.com/dotnet/sdk/blob/v10.0.402/eng/Version.Details.xml#L95-L110
[roslyn-parse-options]: https://github.com/dotnet/dotnet/blob/3073618cc1ab03586872e1cf3035afd377d66df6/src/roslyn/src/Compilers/CSharp/Portable/CSharpParseOptions.cs#L49-L74
[roslyn-parser]: https://github.com/dotnet/dotnet/blob/3073618cc1ab03586872e1cf3035afd377d66df6/src/roslyn/src/Compilers/CSharp/Portable/Syntax/CSharpSyntaxTree.cs#L429-L459
