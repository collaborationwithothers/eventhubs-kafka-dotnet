# Plan: clear failure when the LoadGen Kafka fixture finds no Docker daemon

Issue: [#444](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/444). Pull request short name: `docker-unreachable-message`. Revision 1.

When Testcontainers detects no Docker daemon, LoadGen's Docker-tier tests fail with a `NullReferenceException` instead of a message naming Docker. This plan adds one null check, owned by the LoadGen `LocalKafkaFixture`, that throws `InvalidOperationException` with the message `Docker daemon not reachable; this test is Tier=Docker`, and uses it at the two places where the null is dereferenced today. No test is skipped, retagged or loosened, and nothing changes when Docker is reachable.

## 1. Goal and authority

Goal: on a machine where Testcontainers finds no Docker daemon, each LoadGen test that reaches the local Kafka fixture fails with a message naming Docker and the `Tier=Docker` trait, not with a `NullReferenceException`. The tests keep their `Tier=Docker` trait and still run and fail in the full gate.

Authority, in creation order:
- The issue body of #444 (`updated_at` 2026-11-13T07:13:17Z). The issue has no comments at the time of this revision (comment count 0), so no decision or approval comment changes it. `ready-for-agent` was applied at 2026-11-13T07:13:17Z (issue events). No comment names an implementing tool, so the tool is Claude Code by default (AGENTS.md "Implementer and reviewer").
- There is no split, no parent item and no item plan. The whole issue is one pull request. There is no item state file (`items/444/state.md` does not exist on `main` at 675f837277824d197ce922511b9f55c7d77623b2), which is the normal case before the first implementation pull request (docs/agents/factory.md "Item state file").

Every path this pull request changes is outside the protected paths of `.github/CODEOWNERS` lines 5 to 19 (`tests/Parchwell.LoadGen.Tests/` and `items/444/` match none of them), so GOVERNANCE "Merge rule" in AGENTS.md decides whether an agent may merge it.

### 1.1 What the planning session measured

Planning session, Claude Code cloud sandbox, 2026-11-13, `main` at 675f837, no file at `/var/run/docker.sock`. Command: `dotnet test tests/Parchwell.LoadGen.Tests --no-build -- --filter-namespace Parchwell.LoadGen.Tests.Abrupt --filter-trait "Tier=Docker"`. Result: total 4, failed 3, succeeded 1. The three failures are the three tests the issue names, but they fail at two different places:

| Test | Top frame of the `NullReferenceException` |
|---|---|
| `B10AbruptEndToEndTests.Given_RealKafka_When_ParentChildAndSuccessorRunOnce_Then_ConfirmedJournalMembersAreResentWithIdenticalBytes` | `LocalKafkaFixture.BoundedDockerAuthentication.get_Endpoint()`, `tests/Parchwell.LoadGen.Tests/Component/LocalKafkaFixture.cs:120`, reached from `ContainerBuilder.Build()` at line 21 |
| `B10AbruptOfflineBrokerOutputTests.Given_BrokerStartupOutput_When_StartupIsCanceled_Then_OutputSurvivesFixtureFailureCleanup` | `B10OfflineImageRun..ctor`, `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptOfflineImageTests.cs:82`, called from `B10AbruptOfflineBrokerOutputTests.cs:41` |
| `B10AbruptOfflineBrokerOutputTests.Given_StartedBroker_When_LaterFailureCollectsOutputAndCleanupRuns_Then_BrokerTailRemainsAvailable` | `B10OfflineImageRun..ctor`, `B10AbruptOfflineImageTests.cs:82`, called from `B10AbruptOfflineBrokerOutputTests.cs:64` |

`B10AbruptOfflineImageTests.cs:82` is the field initializer `TestcontainersSettings.OS.DockerEndpointAuthConfig.GetDockerClientBuilder()` of `B10OfflineImageRun`, which runs before that class ever calls `LocalKafkaFixture.StartAsync` (line 497). A check inside the fixture alone therefore leaves two of the issue's three tests failing with a `NullReferenceException`. To meet the issue's goal for the tests it names, this plan applies the same check at line 82 as well (requirement R3). The succeeding test was `B10AbruptOfflineImageTests.Given_BuiltImage_When_ParentChildAndSuccessorRunInContainers_Then_JournalExitAndResendsAreObserved`, which returns at its line 30 when `PARCHWELL_B10_OFFLINE_IMAGE` is unset, before it constructs `B10OfflineImageRun`.

### 1.2 Cause, verified against the pinned source

The issue marks the cause as an inference to check against Testcontainers 4.16.0. `tests/Parchwell.LoadGen.Tests/packages.lock.json` lines 5 to 8 resolve `Testcontainers` 4.16.0. Checked in tag `4.16.0` of https://github.com/testcontainers/testcontainers-dotnet (commit df0f931ea92111ddd96f96fce22ff85b69a58f64), read by a sparse `git clone` in the planning session on 2026-11-13:
- `src/Testcontainers/Configurations/TestcontainersSettings.cs` lines 33 to 39: the provider is the first of `DockerEndpointAuthProviders` for which `IsApplicable() && IsAvailable()` holds, and the configuration is `DockerEndpointAuthProvider?.GetAuthConfig()`, marked `[CanBeNull]`. Both are `static readonly`, so they are computed once per test process.
- `src/Testcontainers/Builders/DockerEndpointAuthenticationProvider.cs` lines 31 to 56: `IsAvailable()` returns true only when a ping of the provider's endpoint succeeds.
- `TestcontainersSettings.cs` lines 158 and 159: `OS` is `new Unix(DockerEndpointAuthConfig)` on Linux, and `src/Testcontainers/Configurations/Unix.cs` lines 112 to 124 return that value unchanged from `DockerEndpointAuthConfig`.
- `` src/Testcontainers/Builders/AbstractBuilder`4.cs `` lines 148 to 154 and 176 to 188: Testcontainers' own builder validation throws a "Docker is either not running or misconfigured" exception when the configuration is null. The fixture's wrapper (`LocalKafkaFixture.cs` line 20) is never null, so that guard does not fire, and the wrapper's `original.Endpoint` dereferences null at line 120.

Confirmed: when no provider's endpoint answers a ping at detection time, `TestcontainersSettings.OS.DockerEndpointAuthConfig` is null on Linux. Inferred, not stated by the source: that every way a sandbox lacks Docker reaches this null path. The pull request does not depend on that inference; it handles the null value wherever it comes from.

## 2. Scope

Files this pull request may change:
- `tests/Parchwell.LoadGen.Tests/Component/LocalKafkaFixture.cs`
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptOfflineImageTests.cs`, only the `docker` field initializer of `B10OfflineImageRun` (lines 82 and 83 on `main`).
- One new test file under `tests/Parchwell.LoadGen.Tests/Component/` for the tests of section 7. Its file name is the implementer's choice.
- `items/444/` (this plan, the authored receipt, the item state file under docs/agents/factory.md "Item state file").

Must not change:
- Every `[Trait]` attribute and every existing test method, assertion and `InlineData` in `tests/`.
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptOfflineBrokerOutputTests.cs`. Its line 91 also dereferences `TestcontainersSettings.OS.DockerEndpointAuthConfig`, but it runs only inside `AssertBrokerRemovedAsync`, which both callers reach after constructing `B10OfflineImageRun` (lines 41 and 64); with R3 that constructor already throws when the configuration is null.
- `tests/Parchwell.Verifier.Tests/` (protected: `.github/CODEOWNERS` line 16) and `tests/Parchwell.Kafka.Tests/`. Their fixtures wrap the same setting (`tests/Parchwell.Verifier.Tests/Component/LocalKafkaFixture.cs:44`, `tests/Parchwell.Kafka.Tests/PhantomSpike/Infrastructure/BrokerEnvironment.cs:41`), but the issue covers LoadGen only.
- `src/`, `scripts/`, `docs/`, `.github/`, `AGENTS.md`, `CLAUDE.md`, `results/`, `scenarios/`, every `.csproj`, `Directory.*.props`, `global.json`, `Parchwell.slnx` and every lock file. No new project, assembly reference or NuGet package (AGENTS.md "Code Review Rules").

## 3. External contracts

### 3.1 The failure

- Exception type: `System.InvalidOperationException`, the type the same file already uses for a fixture configuration failure (`LocalKafkaFixture.cs` line 29) and that `B10OfflineImageRun` uses for `b10_offline_image_docker_unreachable` (`B10AbruptOfflineImageTests.cs` lines 97 to 99).
- `Message`, exactly, with no trailing period or newline: `Docker daemon not reachable; this test is Tier=Docker` (the issue body, "Proposal").
- `InnerException`: null. There is no underlying exception to wrap; the setting is simply null.
- The literal message appears once in the source, in `LocalKafkaFixture` (the rule's owner). Other call sites reach it through the check of 3.2, not by copying the text.

### 3.2 The check

`LocalKafkaFixture` gains one `internal static` method. Its name is the implementer's choice; section 7 calls it `RequireDockerEndpoint` for readability. Shape:
- Parameter: `IDockerEndpointAuthenticationConfiguration?` (the detected configuration).
- WHEN the argument is null THEN it throws the exception of 3.1.
- Otherwise it returns the argument itself, the same reference, with no wrapping or copying.
- It reads no static state, makes no Docker or network call and does no I/O. The caller passes `TestcontainersSettings.OS.DockerEndpointAuthConfig`.
- It checks only whether the configuration is null. It does not inspect `Endpoint`, `Version` or `AuthProvider`.

### 3.3 Unchanged

- `LocalKafkaFixture.StartAsync` signature, `BootstrapServers`, `OutputTailAsync`, `TailOutput`, `DisposeAsync`, `Image`, and the container definition (lines 21 to 57).
- `BoundedDockerAuthentication`'s behaviour for a non-null configuration: same `Version`, `Endpoint`, `AuthProvider`, and the 2-minute client timeout (lines 115 to 125).
- `B10OfflineImageRun`'s Docker client: same builder, same 2-minute timeout (line 83).

## 4. Requirements

- R1. WHEN the check of 3.2 receives null THEN it throws `InvalidOperationException` whose `Message` equals `Docker daemon not reachable; this test is Tier=Docker` and whose `InnerException` is null.
- R2. WHEN `LocalKafkaFixture` is constructed (through `StartAsync`) THEN it passes `TestcontainersSettings.OS.DockerEndpointAuthConfig` through the check before `new ContainerBuilder(Image)` runs, and wraps the returned value in `BoundedDockerAuthentication`. So with no detected configuration, `StartAsync` throws the exception of 3.1 from construction (line 65 on `main`), before its `try` block: no container is built, `startupFailureOutput` is not called and no cleanup runs, because nothing was created.
- R3. WHEN `B10OfflineImageRun` is constructed THEN its `docker` field is built from the value the check returns for `TestcontainersSettings.OS.DockerEndpointAuthConfig`. So with no detected configuration, the constructor throws the exception of 3.1 instead of a `NullReferenceException`.
- R4. WHEN the check receives a non-null configuration THEN it returns that same reference, and the fixture and `B10OfflineImageRun` behave exactly as on `main`.
- R5. WHEN the full gate (`./scripts/verify-dotnet.sh` with no options) runs on a machine with Docker THEN every LoadGen `Tier=Docker` test still runs and passes as on `main`, and the new tests of section 7 run and pass.
- R6. WHEN the LoadGen `Tier=Docker` tests run on a machine where Testcontainers detects no Docker daemon THEN each of the three tests of section 1.1 fails, its failure text contains `Docker daemon not reachable; this test is Tier=Docker`, and the run output contains no `NullReferenceException`. No test reports skipped.

## 5. Invariants and safety rules

- No test is skipped, deleted, retagged or loosened (docs/engineering/dotnet.md "Testing", rules 2 and 4). The check fails; it never calls `Assert.Skip` or returns early.
- The new tests carry no `Tier` trait, so they run in the fast tier and in the full gate (docs/engineering/dotnet-verification.md "Tiers, partial runs and failure reporting"). They need no Docker daemon: they construct `DotNet.Testcontainers.Configurations.DockerEndpointAuthenticationConfiguration`, whose constructor only stores its arguments (tag 4.16.0, `src/Testcontainers/Configurations/AuthConfigs/DockerEndpointAuthenticationConfiguration.cs` lines 32 to 36), and they never read `TestcontainersSettings`.
- Tests do not assign `TestcontainersSettings.OS`, a process-wide settable property (`TestcontainersSettings.cs` line 158), because other tests in the same process read it.
- Analyzer warnings stay errors; the new test file keeps the repository's `Given_<state>_When_<action>_Then_<outcome>` names, with the `CA1707` pragma used by the other LoadGen test files (for example `B10AbruptOfflineBrokerOutputTests.cs` line 10).
- No Azure resource, workflow, secret or result is touched (AGENTS.md hard safety rules 1 to 9).

## 6. Standing fixture rule

This plan adds a check. When an existing test's fixture is rejected by the new check, the implementer does exactly one of:
- (a) changes the fixture so it passes the new check, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's check produces.

The rule never permits removing a case, loosening an assertion or changing a check. The authored receipt lists each change with the check that forces it, and the cold review and Codex review check each listed change. Modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2. Expected count: zero. On a machine with Docker the check returns its input, and no existing test passes null to it.

## 7. Test map

New test class `Parchwell.LoadGen.Tests.Component.LocalKafkaFixtureDockerEndpointTests`, no `Tier` trait, in the new file of section 2. `RequireDockerEndpoint` stands for the check of 3.2, whatever its name.

| Requirement | Test | Input | Literal expected values |
|---|---|---|---|
| R1 | `Given_NoDetectedDockerEndpoint_When_RequiringEndpoint_Then_ThrowsDockerNotReachable` | `null` | `Assert.Throws<InvalidOperationException>` (exact type); `Message` equals `Docker daemon not reachable; this test is Tier=Docker` (ordinal `Assert.Equal`); `InnerException` is null |
| R4 | `Given_DetectedDockerEndpoint_When_RequiringEndpoint_Then_ReturnsSameConfiguration` | `IDockerEndpointAuthenticationConfiguration detected = new DockerEndpointAuthenticationConfiguration(new Uri("unix:///var/run/docker.sock"));` | `Assert.Same(detected, RequireDockerEndpoint(detected))` |
| R2, R3 | No offline unit test. The wiring reads a process-wide static computed once (section 1.2), which a test cannot set to null without affecting other tests (section 5). Covered by acceptance A3 on a machine without Docker and A4 in hosted CI with Docker. | | |
| R5 | Acceptance A4 | | |
| R6 | Acceptance A3 | | |

Boundaries: the check has one condition, null or not null, and both sides have a test. There is no empty input other than null.

## 8. Deployment assumptions

None. The pull request changes test code only; no deployed container, image, chart or runtime reads it. Plan rule 3 of the `plan-pr` skill therefore has nothing to apply to.

## 9. Plan-check questions (docs/agents/factory.md "Item lifecycle" step 1)

- (a) Steps whose effort scales with a number unknown at planning time: none. The change touches two call sites, both counted in sections 1.1 and 2 by `grep -rn DockerEndpointAuthConfig tests/Parchwell.LoadGen.Tests` (three hits, lines 20, 82 and 91; line 91 is excluded with its reason in section 2). The standing fixture rule expects zero changes (section 6); if any appears, each is listed in the receipt, and more than zero is not a cap exhaustion but a fact the receipt records.
- (b) Where the plan is stricter than its parent: there is no parent plan. Against the issue body, the plan is wider in one place: it also applies the check at `B10AbruptOfflineImageTests.cs:82`, because section 1.1 measured two of the issue's three named tests failing there and not in the fixture. It pins the exception type and an empty `InnerException`, which the issue leaves to the implementer, so the tests have literal expected values.
- (c) Tests or checks that depend on code, data or behaviour neither on `main` nor in the pull request: the null value of `TestcontainersSettings.OS.DockerEndpointAuthConfig` without Docker, checked against the Testcontainers 4.16.0 source (section 1.2) and against recorded output (section 1.1). Acceptance A4 depends on the hosted runner providing Docker, as the existing Docker-tier tests already do on `main`. No deployment assumption follows (section 8).

## 10. Acceptance commands

- A1. `./scripts/verify-dotnet.sh --tier fast`. Evidence: the line `Partial verification passed (tier fast, projects: all). ...` and the two tests of section 7 listed as passed in the LoadGen `results.xml` of the printed results directory. Cited as a partial run, not the gate.
- A2. `dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj -- --filter-class Parchwell.LoadGen.Tests.Component.LocalKafkaFixtureDockerEndpointTests`. Evidence: 2 tests, 2 passed, 0 skipped, on a machine without Docker.
- A3. On a machine where `/var/run/docker.sock` is absent and `docker version` fails: `dotnet test --project tests/Parchwell.LoadGen.Tests/Parchwell.LoadGen.Tests.csproj -- --filter-namespace Parchwell.LoadGen.Tests.Abrupt --filter-trait "Tier=Docker"`. Evidence: total 4, failed 3, succeeded 1, matching section 1.1; each failed test's text contains `Docker daemon not reachable; this test is Tier=Docker`; `grep -c NullReferenceException` over the output prints 0. The receipt records that this evidence was taken with no Docker daemon, which is what A3 tests.
- A4. Hosted CI runs `./scripts/verify-dotnet.sh` with no options on the pull request's final head (`.github/workflows/dotnet-verification.yml`). Evidence: the run link and its pass, which shows R5. A local full-gate run in a sandbox without Docker or cgroup v2 fails on `Tier=Docker` and `Tier=Host` tests and is not evidence for R5.
- A5. `git diff --name-only origin/main...HEAD` lists only paths allowed in section 2.

## 11. Sources

- Issue #444 body: https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/444
- Testcontainers for .NET, tag 4.16.0, commit df0f931ea92111ddd96f96fce22ff85b69a58f64, files and lines in sections 1.2 and 5: https://github.com/testcontainers/testcontainers-dotnet/tree/df0f931ea92111ddd96f96fce22ff85b69a58f64/src/Testcontainers
- On `main` at 675f837277824d197ce922511b9f55c7d77623b2: `tests/Parchwell.LoadGen.Tests/Component/LocalKafkaFixture.cs`, `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptOfflineImageTests.cs`, `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptOfflineBrokerOutputTests.cs`, `tests/Parchwell.LoadGen.Tests/packages.lock.json`, `.github/CODEOWNERS`, `docs/engineering/dotnet.md` "Testing", `docs/engineering/dotnet-verification.md` "Tiers, partial runs and failure reporting".
- Planning-session test run of section 1.1 (recorded output, not committed).

No Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour is relied on.

## 12. Open questions for Hari

None.

## 13. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract of section 3, a requirement of section 4 or the scope of section 2, for example to change a file listed under "Must not change" or to add a test trait.
