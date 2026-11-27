# Cached test state: contract-grade plan

Revision: r2. Base commit: `46a58ddd43d365486d5448f1cf5de35b696e33c3` on `main`.

One pull request removes the only test-side state that survives from one mutant to the next in the verifier mutation campaign: three static caches in the consumer phase judge tests. It then measures, locally, which accepted kills that rested only on those tests still hold. Accepting the small timing risk this change adds to the runtime qualification is Hari's decision, asked under "Open questions for Hari".

## 1. Goal and authority

Goal: each mutant's test run builds the consumer phase judge inputs while that mutant is active, so a kill or survival no longer depends on which mutant ran earlier in the same test process. Then measure the effect on the kill sets the issue names.

Authority, in creation order:

1. The body of issue #601 (scope items 1 to 5 and its constraints).
2. [#601 decision 7211787887](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/601#issuecomment-7211787887), which adopts [#598 decision 7184653835](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/598#issuecomment-7184653835) as the pattern. Point 1: `ready-for-agent` approves the revision whose newest fresh check states "Blocking findings: none" and whose open questions read None, and the control session records that approval with revision and commit. Point 2: every open question goes to Hari before approval. Point 3: Claude Code implements on `claude-sonnet-5-5`; protected-path merges stay with Hari.
3. The issue body cites [#599 decision 7193808224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/599#issuecomment-7193808224) point 2 (batched acceptance runs) and [#600 findings 7201011493](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/600#issuecomment-7201011493) section 2 as context. They are read as evidence here; no decision on #601 links and adopts them beyond the issue body's constraint.

There is no item plan and no parent plan: this is a mutation-harness fix issue, and the issue body is the specification.

## 2. Scope

The pull request is named "Cached test state". Its branch is `claude/601-cached-test-state`.

Files allowed to change:

| File | Protected | Change |
|---|---|---|
| `tests/Parchwell.Verifier.Tests/Judge/ConsumerPhaseJudgeTests.cs` | yes (`tests/Parchwell.Verifier.Tests/**`) | Requirements R1 to R3 |
| `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` | yes | Requirement R4, written only by the `renew` command |
| `docs/operations/verifier-mutation.md` | no | Requirement R5 |

Paths that must not change: everything under `src/`; every other file under `tests/`; `tests/Parchwell.Verifier.Tests/Mutation/*.py`; `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`; `tests/Parchwell.Verifier.Tests/stryker-config.json`; `tests/Parchwell.Verifier.Tests/Parchwell.Verifier.Tests.csproj`; `.github/**`; `.config/dotnet-tools.json`; `scripts/**`; `results/**`; every `scenarios/*/expected.yaml`; AGENTS.md, CLAUDE.md and `docs/` outside `docs/operations/verifier-mutation.md`.

Out of scope: any threshold, test-set or gate change; any added mutation job; any change to the 4,200-second limit or the 2,700-second `budget_seconds`; any hosted dispatch; the #600 fast-suite proof (section 11, item D2).

## 3. Inventory (issue scope item 1)

Search: every `.cs` file compiled into the mutation-only build (`ParchwellVerifierMutationOnly=true`, which removes `Component/**/*.cs`, `Support/ComponentEvidenceBuilder.cs`, `Host/VerifyRunHandlerTests.cs`, `Host/VerifierHostTests.cs` and `Host/B10AbruptRejectionTests.cs`; `Parchwell.Verifier.Tests.csproj` lines 20 to 22), 145 tracked test-project files plus the linked `tests/fixtures/ProbeV2MockCluster.cs`, 146 files in all (counted by `git ls-tree` and by the A1 `find`), at the base commit. It looked for static fields and properties with initializers, static `Lazy`, static constructors, statics assigned outside their declaration, `[ThreadStatic]`, `AsyncLocal`, `ThreadLocal`, static dictionaries and caches, module initializers, class, collection and assembly fixtures, `IAsyncLifetime`, and `MemberData` and `ClassData` sources. A value counts only when it is produced by running code in one of the 31 target files that `TARGETS` lists (`tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py` lines 27 to 58).

Result: three affected entries, all in one class.

| File:line | Member | Target code it runs | Lifetime | Test methods that read it |
|---|---|---|---|---|
| `Judge/ConsumerPhaseJudgeTests.cs:22` | `BaseC0 = new(() => BuildAsync("C03", "C0"))` | `BuildAsync` (line 729) runs `HostEvidenceBuilder.Verify(...)` (`Support/HostEvidenceBuilder.cs` lines 211 to 214), which constructs `VerifyRunHandler` (a target) and reaches `A01EvidenceRecorder`, `WorkloadRecorder`, `BatchingEvidenceRecorder`, `PublicationAcceptance`, `DuplicateBreakdown`, `VerifierResultCodec`, `OutcomeClassifier` and `DuplicateClassifier`, five times | process static | 20 |
| `Judge/ConsumerPhaseJudgeTests.cs:23` | `BaseC1 = new(() => BuildAsync("C10", "C1"))` | same | process static | 7 |
| `Judge/ConsumerPhaseJudgeTests.cs:132` | `BaseB04 = new(() => BuildAsync("B04", "C0", addConsumer: false))` | same | process static | 4 |

30 of the 31 test methods in the class read a cache; one test method reads two. The one that reads none is `Given_TypedInputs_When_EvaluatedDirectly_Then_ProducesTheRowsOfTheJudgeTests`.

A second hazard of the same caches: `BuildAsync` asserts `Assert.Equal(0, exit)` (line 740) and `Assert.True(...)` (line 742). A `Lazy<Task<...>>` keeps a faulted task. If the first build throws under one mutant, every later reader in that process fails under every later mutant.

Examined and not affected, each built only from literals, BCL calls, or constructors of types outside the target files: about 150 static readonly fields and static get-only properties (for example `RunIdentity`, `InputLimits`, `OutputLimits`); `PhaseIdentity.JobName` callers (`Judge/PublicationAcceptanceTests.cs:20-22`, `Window/B10RunWindowTests.cs:235-236`), because `Common/Phase/PhaseIdentity.cs` is not a target; the static dictionaries in `IdleObservation/IdleFixture.cs:13` and `SingleAction/SingleActionFixture.cs:14`; and every `MemberData` source. There are no class, collection or assembly fixtures in the mutation-included tests; the class fixtures under `Component/` are removed from the mutation build. `[CollectionDefinition("VerifierEnvironment")]` (`Host/ChartEnvironmentVerifierHostTests.cs:18-19`) carries no state.

Static state inside target files is already handled by "Issue 340 static-field isolation" in docs/operations/verifier-mutation.md and is not in this scope.

The inventory was taken by a reader subagent with the search commands in section 9, acceptance command A1. The planning session checked the three entries and the `HostEvidenceBuilder.Verify` chain at the cited lines.

## 4. External contracts

This pull request changes no wire format, failure name, stage name, configuration key, CLI flag, Kafka client setting or verifier predicate. It changes no test input or expected value. The contracts it touches:

- **Qualification file.** `c5-runtime-qualification.json` changes only by running `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py renew --head <HEAD_SHA> --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json --basis "<BASIS>"`, where `<HEAD_SHA>` is the commit that holds the test change. `<BASIS>` is exactly:

  `Issue 601 builds the ConsumerPhaseJudgeTests inputs per test instead of in static Lazy caches. Local mutation-only suite wall, 4-core container, idle, fresh process per run, 2 runs each on 2026-11-27: 87.66 s and 87.97 s before, 89.01 s and 89.44 s after. Timing risk accepted by <DECISION_URL>.`

  `<DECISION_URL>` is the URL of Hari's decision that answers question Q1 in section 10. A revision of this plan that applies that answer replaces the placeholder with the URL. Until then, the plan is not approvable.
- **Runbook text.** The new runbook section (R5) is operational documentation that later sessions read as the record of this fix.

## 5. Requirements

R1. WHEN any test in `ConsumerPhaseJudgeTests` needs the C03/C0, C10/C1 or B04/C0 base stores THEN it obtains them by calling `BuildAsync` with exactly the arguments the cache used (`("C03", "C0")`, `("C10", "C1")`, `("B04", "C0", addConsumer: false)`) during its own execution. The shape is fixed, because it is the shape section 7 measured: each of the 27 reads `BaseC0.Value`, `BaseC1.Value` and `BaseB04.Value` at the base commit becomes exactly one call to `BuildAsync` with that cache's arguments, at the same place in the same test method or helper, either written directly or through a member that calls `BuildAsync` on every access and stores nothing. No build runs in the test class constructor, and no test that read no cache at the base commit builds. No static, and no object that outlives one test, holds `Stores`, a `Task<Stores>` or any value derived from them. The fields `BaseC0`, `BaseC1` and `BaseB04` are removed or no longer cache. The method is per-test construction; the reasons are in section 7.

R2. WHEN the suite runs THEN every one of the 111 test cases of `ConsumerPhaseJudgeTests` at the base commit still exists with the same name, the same data rows and the same assertions, and passes. The change to each test body is limited to how it obtains the base stores. `.Copy()` calls stay where they are or are made redundant only because the stores are freshly built; no assertion is added, loosened, deleted or skipped.

R3. WHEN `BuildAsync` throws during one test (for example because the active mutant makes the verify handler return a nonzero exit) THEN only that test fails; the next test builds again. This follows from R1 and needs no extra code.

R4. WHEN the pull request is opened THEN the qualification file has been renewed with the command and basis in section 4, and `c5_runtime.py check --base <merge base> --head <HEAD>` does not report `rejected`. The renewal is a separate commit after the test change, so `<HEAD_SHA>` in the basis command names the commit that holds the test change. If a later commit changes the test file again, `renew` runs again on that commit with the same basis.

R5. WHEN the pull request merges THEN docs/operations/verifier-mutation.md has a new section `## Issue 601 test-side cached state`, placed directly after "Issue 340 static-field isolation". It states, in this order: the mechanism (one reused MTP test host per runner; the active mutant read on every `IsActive` call; a test-side static cache keeps the mutant active when it was built, including a faulted build); the inventory of section 3 with file and line; the fix (per-test construction); the measured suite times of section 4; that test-side static state built from target code is not detected by any check, so a new test-side cache reintroduces the fault; and a link to the measurement comment of R6 once posted. It changes no other section. The existing sentence "Mutable static state that Stryker does not mark is not detected" stays.

R6. WHEN the test change is committed on the pull request branch THEN the implementer runs measurement M1 (section 8) on that commit and posts its result as one comment on issue #601, which the pull request description links. A mutant that turns from Killed to Survived is a finding recorded in that comment; it does not change any threshold, test, assertion or expected outcome (issue scope item 4).

R7. WHEN the acceptance search A1 finds an affected entry that section 3 does not list THEN the implementer stops under the stop rule (section 12).

## 6. Invariants and safety rules

- No assertion is loosened, deleted or skipped (issue scope item 4; AGENTS.md "Code Review Rules"; docs/engineering/dotnet.md "Testing").
- No hosted workflow is dispatched (issue constraint). Mutation acceptance for this change is the batched run of [#599 decision 7193808224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/599#issuecomment-7193808224) point 2 under docs/agents/factory.md "Evidence pack".
- No target source changes, so the comparison in M1 stays bound to the accepted campaign's sources.
- The pull request touches protected paths; Hari merges it (AGENTS.md GOVERNANCE "Merge rule").
- Agents commit no program output: M1's raw reports and logs stay outside the repository; only the summary comment of R6 is published.
- Fixtures and expected values. This plan adds or tightens no check, so plan-pr "Plan rules" rule 2 does not apply. Any change to a fixture, data row or expected value, beyond how a test obtains its base stores under R1, triggers the stop rule (section 12).

## 7. Method choice and its evidence (issue scope item 2)

Per-test construction is chosen. Evidence, read by a reader subagent in clones at the pinned commits and spot-checked by the planning session:

- Stryker.NET 5.0.0 on MTP reuses one live test server per runner: `GetOrCreateServerAsync` returns the existing server "if (existing.IsAlive)" ([MicrosoftTestingPlatformRunner.cs L773-L783](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.TestRunner.MicrosoftTestPlatform/MicrosoftTestingPlatformRunner.cs#L773-L783)). Confirmed.
- Before each run it writes the mutant id to a memory-mapped file; "The injected MutantControl maps the same file and reads the id on every IsActive call, so the reused test host always sees the current mutant" ([MicrosoftTestingPlatformRunner.cs L131-L147](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.TestRunner.MicrosoftTestPlatform/MicrosoftTestingPlatformRunner.cs#L131-L147); [MutantControl.cs L461-L483](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/InjectedHelpers/MutantControl.cs#L461-L483)). Confirmed.
- MTP tests one mutant per test run: "The MTP runner can only activate a single mutant per test run" ([MutationTestProcess.cs L174-L181](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/MutationTest/MutationTestProcess.cs#L174-L181)). Confirmed.
- xUnit v3 "creates a new instance of the test class for every test that is run, so any code which is placed into the constructor of the test class will be run for every single test" ([shared-context.md L13](https://github.com/xunit/xunit.net/blob/7101d170582b391dad779a841737acf31876b809/site/docs/shared-context.md#L13)). Confirmed.
- Inferred from the above: code run inside a test, or in its class constructor, runs within that mutant's test run and reads that mutant as active.

Rejected alternatives:

- Class fixture (`IClassFixture` with `IAsyncLifetime`). It would build once per class per run request, so cheaper. Its correctness rests on an inference the planning session did not test: that xUnit v3 creates class fixtures anew for each MTP run request in the same process (the reader traced `TestPlatformTestFramework.OnRequest` building a new `ProjectAssemblyRunner` per request, [TestPlatformTestFramework.cs L256-L275](https://github.com/xunit/xunit/blob/0c7aa4991fa340689155d52043ce88f52f2f7421/src/common/MicrosoftTestingPlatform/TestPlatformTestFramework.cs#L256-L275), but did not run it). The measured cost of per-test construction is small (below), so the cheaper method does not justify that unverified dependency.
- Test-side reset hook. Stryker gives the test process no per-run signal: the MutantControl comment states the host "has no per-run reset hook" ([MutantControl.cs L17-L24](https://github.com/stryker-mutator/stryker-net/blob/6e77a3451bac4793e9c839c3ff4055c1b30ca3af/src/Stryker.Core/Stryker.Core/InjectedHelpers/MutantControl.cs#L17-L24)). Confirmed that no hook exists; a hook would have to read Stryker internals.

Effect on suite time, measured locally by the planning session on 2026-11-27 at the base commit, Release, `-p:ParchwellVerifierMutationOnly=true`, xUnit native runner, 4-core Linux container, idle, each run a fresh process. The "after" build used a scratch prototype that turned the three fields into accessors that call `BuildAsync` on every read; it was not committed.

| Measurement | Before (cached) | After (per test) |
|---|---|---|
| `ConsumerPhaseJudgeTests` only, wall, 2 runs | 2.62 s, 2.81 s | 10.03 s, 9.69 s |
| Whole mutation-only suite, wall, 2 runs | 87.66 s, 87.97 s | 89.01 s, 89.44 s |

All 111 cases passed in each class run. The suite mean rose by 1.41 s, 1.61 percent (computed by script). The class runs in parallel with other classes, so the suite cost is much smaller than the class cost.

Estimate, 2026-11-27, basis: the 1.61 percent local suite increase, applied to the longest hosted shard observation recorded in docs/operations/verifier-mutation.md (2,637.073 s on `phase-candidate-codec-p20`), assuming per-mutant time scales with suite wall and that a hosted runner behaves like this container, neither measured: about 2,679 s against the 2,700-second `budget_seconds`. The `check` command does not re-evaluate estimates after a renewal (docs/operations/verifier-mutation.md, version 2 `renew`: "Measured maps, timing evidence, estimates, targets and historical entries are not touched"), so the real effect appears only in the next hosted campaign. This is the risk question Q1 asks Hari to accept.

## 8. Measurement M1 (issue scope item 3)

Purpose: check, after the fix, every accepted kill that rested only on the cached tests.

Reference: the accepted campaign, main run [45555996117](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45555996117) and companion [45555978345](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/45555978345), both on `e3c8eb9462a6ecae139a651c5eef39812be07801`. On 2026-11-27 the API listed 357 and 292 artifacts, none expired, earliest expiry 2027-02-23.

Steps:

1. Download the `c5-mutation-` artifacts of both runs and reconcile them by script against the coordinator `summary.json`: 9773 Killed, 1 Timeout, 2941 Survived, 1854 CompileError, 601 Ignored, as #600 reproduced. A mismatch stops M1; record it in the R6 comment and continue the pull request.
2. Select by script every mutant whose hosted status is Killed and whose `killedBy` list names only tests of `Parchwell.Verifier.Tests.Judge.ConsumerPhaseJudgeTests`. Record the count. #600 counted 165; a different count is recorded with both numbers, and M1 continues with the computed set.
3. Group the selection by target. For each target, choose one untouched native report of that target from the reference artifacts as its inventory, and map each selected mutant to that report's ID by identity (mutator, replacement, location and static flag) before the call; numeric IDs from other shard reports are never used directly. Run one `c5_runtime.py local-sample` invocation on a clean checkout of the pull request commit that holds the test change, with `--target` set to the target, `--inventory` set to that report, `--inventory-source-commit e3c8eb9462a6ecae139a651c5eef39812be07801`, the mapped IDs as `--selection`, `--timeout 5400`, and `--output-dir` set to a new directory that does not exist yet, outside the repository and outside any Git worktree, one per invocation, under the session's scratch space. `local-sample` runs static mutants in fresh processes itself.
   - If `local-sample` refuses the target because its committed bytes differ from the reference commit, record every selected mutant of that target as not measured.
   - If it refuses the selection for any other reason before running a mutant (for example `sample_plan` rejecting a span that cannot be expressed, or a static mutant sharing its span), run that target again once per selected mutant, sequentially, each with a one-ID selection and its own new output directory. A mutant whose one-ID invocation is also refused is recorded as not measured, with the refusal message.
   - If an invocation starts and fails or times out, record its unmeasured mutants with the reason and do not retry it.
4. Compare each selected mutant's local status with its hosted status by identity: mutator, replacement, location and static flag, as in #599 and #600. Numeric IDs are not assumed to match.
5. Compute by script: the number of mutants that stayed Killed, turned Survived, turned Timeout or other; and the score that the accepted campaign would show if each measured change were applied, labelled arithmetic from a partial measurement.

Expected values, from #600: the four WorkloadRecorder mutants 13667, 13672, 13677 and 13681 (hosted report IDs) are in the selection and are Survived after the fix, as each was when run alone before the fix. A different status for any of them is a finding that contradicts the #600 diagnosis; record it, do not change code for it. If one of them ends not measured, the R6 comment says so, and R1 and R3 then rest on the remaining expected survivors and on A1.

Caps: per target, at most one batch invocation plus, only after a refusal under step 3, one one-ID invocation per selected mutant of that target; the fresh static children `local-sample` starts itself are part of their invocation. Each invocation at most 5,400 s; all M1 invocations together at most 14,400 s of wall time. Exhaustion action: `record the gap and continue`. The R6 comment lists each unmeasured mutant with its reason. Estimate, 2026-11-27, basis: 165 mutants at 37.7 s per mutant (#600, citing #599's local MTP run) plus about 128 s of start-up per invocation, for an unknown number of targets: about 6,200 s plus start-up, more if a refusal forces one-ID invocations.

Why batches are enough: issue scope item 3 allows isolated invocations or whole targets. After the fix, section 3 finds no other test-side state that outlives a mutant, and A1 rechecks it on the pull request head, so one sparse batch per target in a reused process measures each mutant under that mutant. If A1 finds another entry, the stop rule applies before M1.

Limit stated in the R6 comment: M1 measures only one direction. A mutant that Survived in the accepted campaign because its cached inputs were built under no mutant could now be Killed; M1 does not select those, because coverage was `off` and no report lists the tests that ran them. The next batched acceptance campaign (section 11, item D1) measures both directions and the true score.

## 9. Test map and acceptance commands

Test map:

| Requirement | Test or check | Input | Expected |
|---|---|---|---|
| R1 | A1 below | the changed file | no static member of `ConsumerPhaseJudgeTests` holds `Stores`, `Task<Stores>`, `Lazy<...Stores...>` or a value from `BuildAsync`; A1 prints no line |
| R1, R3 | M1 expected values | mutants 13667, 13672, 13677, 13681 | Survived |
| R2 | A2 below | the 111 cases of the class | 111 total, 111 passed, 0 failed, 0 skipped; the sorted list of test case display names equals the base commit's list |
| R2 | A3 below | full Verifier test project | all pass |
| R4 | A4 below | qualification file | `check` status is not `rejected`; exit code 0 |
| R5 | A5 below | runbook | section present, after "Issue 340 static-field isolation" |
| R6 | the R6 comment | M1 output | counts per section 8 step 5, unmeasured mutants listed |

Acceptance commands, from the repository root:

- A1, inventory and R1. Re-run the reader's searches on the pull request head over the mutation-included files and confirm section 3 is complete:
  ```bash
  cd tests/Parchwell.Verifier.Tests
  find . -name '*.cs' -not -path './obj/*' -not -path './bin/*' -not -path './Component/*' \
    | grep -v -E 'Support/ComponentEvidenceBuilder.cs|Host/VerifyRunHandlerTests.cs|Host/VerifierHostTests.cs|Host/B10AbruptRejectionTests.cs' > /tmp/included.txt
  echo ../fixtures/ProbeV2MockCluster.cs >> /tmp/included.txt
  xargs grep -n -E 'IClassFixture|ICollectionFixture|AssemblyFixture|IAsyncLifetime|ThreadStatic|AsyncLocal|ThreadLocal|ConditionalWeakTable|ConcurrentDictionary|Lazy<|LazyInitializer|ModuleInitializer|\?\?=' < /tmp/included.txt
  grep -n -E '^\s*(private|internal|public|protected)?\s*static\s+(readonly\s+)?[^(=]*\b(Stores|Lazy<)[^(=]*\s\w+\s*(=[^>]|;|\{)' Judge/ConsumerPhaseJudgeTests.cs
  ```
  Evidence: the first grep's output, with each hit dispositioned in the PR implementation notes as section 3 does; the second grep prints nothing. The second grep matches static fields and auto-properties only. On the base file it prints exactly lines 22, 23 and 132 (checked by the plan check and the planning session), so it does not match the static methods `Mutated`, `ProducerIncompleteRepeat4`, `KilledC10` and `BuildAsync`.
- A2: `./scripts/verify-dotnet.sh --project Parchwell.Verifier.Tests` is the partial run for the class and project (docs/agents/pull-requests.md "Verification gates" step 1); additionally run the class alone and list its case names before and after by script. Evidence: counts and the name-list comparison in the PR implementation notes.
- A3: `./scripts/verify-dotnet.sh --tier sandbox` once, on the source the cold review cleared (step 3 of "Verification gates"), recorded as a partial run excluding `Tier=CGroupV2`. The hosted `.NET verification` check on the final head is the pre-merge gate; it builds and runs the mutation-only configuration (`scripts/verify-dotnet.sh` `mutation_only_build` and `mutation_only_test`).
- A4: `git fetch --unshallow origin` if the clone is shallow (the check reads pinned historical revisions), then `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py check --base <merge base> --head <HEAD> --qualification tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`. Evidence: its JSON output, which lists the file under `assessed_inputs`, and exit code 0.
- A5: `npm run check:docs`, with each warning fixed or explained (docs/agents/pull-requests.md rule 8).
- Cold review under docs/agents/pull-requests.md rule 8 and CLAUDE.md "Before opening a pull request".

## 10. Open questions for Hari

Q1. Accept the timing risk of the renewal. The change makes each mutant's test run slightly longer. Measured locally: suite wall 87.66 s and 87.97 s before, 89.01 s and 89.44 s after, plus 1.61 percent (section 7). The renewal keeps every shard estimate as measured; the check does not re-time them. Estimate, unmeasured on hosted runners: the longest recorded shard observation would move from 2,637 s to about 2,679 s, under the 2,700-second budget by about 21 s. The 4,200-second campaign limit is not changed by this pull request; whether the next campaign meets it is measured by that campaign. Recommendation: accept, because the fix removes false kills from the gate and its measured cost is small. Alternative: require a hosted timing diagnostic before merge, which needs a dispatch decision and delays the fix. The answer's URL becomes `<DECISION_URL>` in section 4.

Deployment assumptions, none open beyond Q1:

- The reference artifacts stay downloadable until M1 runs. Evidence: the runs' artifact listings on 2026-11-27, none expired, earliest expiry 2027-02-23 (taken over the REST API, idle, fresh request). No test covers it; if they expire, M1 records the gap and continues.
- The local timing in section 7 was taken with a fresh test process per run, on an idle container. It is not hosted evidence; Q1 covers that gap.

## 11. Deferred work

- D1. Mutation acceptance of this change. Evidence: docs/agents/factory.md "Evidence pack" requires a passing `verifier-c5-mutation.yml` run on `main` that includes the change; [#599 decision 7193808224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/599#issuecomment-7193808224) point 2 batches it to the next publication. On the verdict path: yes. Deferral is permitted because a hosted dispatch needs Hari's decision (issue constraint) and the batched run covers this change. Responsible: the control session that prepares the next evidence pack, with Hari's dispatch decision. Stage: after merge. Completion evidence: that run's `summary.json` with `measurement_complete: true` and `break_passed: true`, measured commit including this merge. Gate it blocks: the next evidence-pack publication. It also gives the true score and both kill-set directions that M1 leaves open.
- D2. Redo the #600 fast-suite kill-set proof (issue scope item 5). Evidence: #600 findings section 3, "Fixing that may change kill sets, so the proof must be redone after the fix". On the verdict path: no; it belongs to the fast-suite design that Hari has not yet planned. Responsible: the planning session of that design. Stage: after D1, because the proof needs `killedBy` lists from a campaign run with this fix. Completion evidence: the proof recomputed on D1's reports. Gate it blocks: approval of any fast-suite change.
- D3. Optional: an automated guard against new test-side caches built from target code. Not on the verdict path. It is a new check with heuristic inputs (docs/agents/pull-requests.md rule 5 requires explicit inputs, failure conditions and regression examples), so it is not added here. The runbook section of R5 records the hazard instead. Responsible: none unless Hari asks for it.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract, a requirement or the scope: for example when A1 finds an affected entry section 3 does not list, when the change cannot keep every case and assertion of R2, when `renew` refuses the change or `check` rejects it, or when any file outside section 2's list would have to change.

## 13. Plan-check questions

(a) Steps whose effort scales with a number unknown at planning time: M1's selection size, number of targets and number of refused selections (caps in section 8: one batch invocation per target, one-ID invocations only after a refusal, 5,400 s each, 14,400 s in total, `record the gap and continue`); the A1 hits to disposition (bounded by the 146 included files; 7 lines on the base commit; each hit is one line in the notes, no cap needed).

(b) Where the plan is stricter than its parent: there is no parent plan. Against the issue body, the plan fixes the method (per-test construction) and its shape instead of leaving it open, pins the renewal basis, and adds the four expected survivors of section 8. One difference rather than strictness: M1 runs each target's selected kills in one sparse batch rather than one isolated invocation per mutant; section 8 "Why batches are enough" gives the reason.

(c) Tests or checks depending on code, data or behaviour neither on `main` nor in the pull request: the reference artifacts of runs 45555996117 and 45555978345 (checked by API, section 10); the Stryker and xUnit behaviours of section 7 (checked against source at the pinned commits); the #600 statuses of the four expected survivors (checked against #600 findings section 2, which reports them from isolated local runs; not re-run here).

## 14. Size estimate

Estimate, 2026-11-27, basis: the shape of the change in sections 3 and 5. One pull request, 3 files: `ConsumerPhaseJudgeTests.cs` about 40 changed lines (the three fields and 27 `.Value` read sites, counted by script); `c5-runtime-qualification.json` about 40 to 80 changed lines (assessments in the seven timing contexts that use this file, plus its `shared_inputs` hash); the runbook about 15 added lines. Size is reported, not enforced.

## 15. Sources

- Issue #601 body and [decision 7211787887](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/601#issuecomment-7211787887).
- [#600 findings 7201011493](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/600#issuecomment-7201011493), sections 2 and 3.
- [#599 decision 7193808224](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/599#issuecomment-7193808224), point 2.
- docs/operations/verifier-mutation.md at the base commit: "Runtime qualification diagnostics" (version 2 `renew`, assessments and basis), "Issue 340 static-field isolation".
- `tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py` at the base commit: `renew` (lines 2518 to 2555), `renew_v2` (2573 to 2601), `check` (2464 to 2516), `local_sample` (from 2750), `NON_RENEWABLE` (344 to 345); `c5-runtime-qualification.json` line 302 (this file's hash in `shared_inputs`).
- Stryker.NET at [`6e77a345`](https://github.com/stryker-mutator/stryker-net/tree/6e77a3451bac4793e9c839c3ff4055c1b30ca3af) (tag `dotnet-stryker@5.0.0`) and xUnit docs at [`7101d170`](https://github.com/xunit/xunit.net/tree/7101d170582b391dad779a841737acf31876b809), as cited in section 7. Tool used: Git clones at those commits, read by a reader subagent; the planning session re-read the MicrosoftTestingPlatformRunner, MutationTestProcess, MutantControl and shared-context lines it quotes.
- Local measurements of section 7, taken by the planning session; raw xUnit XML outside the repository, not committed.
