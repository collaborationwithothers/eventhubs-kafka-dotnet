# Plan: capture isolation for the native metric state tests

Issue: [#540](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/540). Pull request short name: `capture-isolation`. Revision 2.

Changes since Revision 1, from [plan check 6958860463](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/540#issuecomment-6958860463):
- Finding 1: the per-test scenario alone was measured (new row in 2.2), and the "What this shows" bullet now states what was measured. The finding's replacement text called the result inferred; the measurement replaces the inference.
- Finding 2: section 9 item 3 now rebuilds the branch before its 10 runs (the check's literal text, with `--filter-trait`).
- Finding 3: the section 12 evidence sentence is replaced with the check's literal text.
- Finding 4: new deterministic test `Given_ForeignMeasurementWithSharedScenario_When_Captured_Then_ItIsExcluded` (8.2, requirement 7), prototyped red on `main` and green with the change.
- P2 `apply in implementation`: requirement 2 and 8.1 now pin `ConcurrentQueue<T>` and `Enqueue`.
- Observation on `--filter`: commands now use `--filter-trait`; both forms selected the same 486 tests (2.2).
- Section 10 size estimate updated; section 13 sources updated.

The absent-counter test fails intermittently because its test-only metric capture is not isolated from other tests. It stores measurements in a plain `List<T>`, and it accepts any measurement tagged `scenario=scenario`, a value other test classes also use. When another test class records on the same meter name on another thread, the two writes race inside `List<T>.Add` and the first value can be lost, which is the CI symptom `Actual: [3, 0]`. This plan changes only the test file: a thread-safe collection and a per-test scenario value. Production code under `src/Parchwell.Kafka/` is not the cause and does not change.

Base: `main` at `35f0fd5f223e32942db4bfd599a69cfd9e7bacac`. All line references are on that commit. The test file last changed in `288f4632ee158aa0f93f229bb406855c72a6ac3a`.

## 1. Goal and authority

Goal: the issue's own goal. Find the root cause, fix it in the test or in the code it covers, and show the fix by repeated runs, without loosening or skipping an assertion.

Authority, in creation order:
- The issue body of #540 (created 2026-11-17T19:24:03Z). There is no item state file (`items/540/` does not exist on `main`), no item plan, no parent and no split. The whole issue is one pull request. No comment names an implementing tool, so the tool is Claude Code by default (AGENTS.md "Implementer and reviewer"). `ready-for-agent` has not been applied (issue events, read 2026-11-17).
- [Decision 6958523164](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/540#issuecomment-6958523164) (2026-11-17T19:25:35Z), which adopts [#499 decision 6958521790](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/499#issuecomment-6958521790): agents merge by merge commit (`merge_method: merge`) through the REST merge route, under AGENTS.md GOVERNANCE "Merge rule".

Catch-up of 2026-11-17: one comment (6958523164), not edited; issue `updated_at` 2026-11-17T19:25:35Z.

Evidence, not authority: [job 134277473583](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/44150584017), head `7b68c167f203d4af5e3afa1442b0bab6d99bc15b`, conclusion `failure`, attempt 1 (`gh api .../actions/jobs/134277473583`, read 2026-11-17). The issue body quotes its assertion: `Expected: [2, 3, 0]`, `Actual: [3, 0]`.

## 2. Root cause

### 2.1 Mechanism

Established from the source on `main`:
- `Capture` (`tests/Parchwell.Kafka.Tests/Statistics/KafkaNativeMetricStateTests.cs` lines 182 to 203) enables every instrument whose meter name is `KafkaStatisticsMetrics.MeterName` (lines 188 to 191). Every `KafkaStatisticsMetrics` instance creates a `Meter` with that one name (`src/Parchwell.Kafka/Statistics/KafkaStatisticsMetrics.cs` lines 14 and 16). So each `Capture` also receives measurements from meters that other tests create.
- The callback keeps a measurement when its `scenario` tag equals the literal `"scenario"` (line 195), and appends it to a `List<...>` (lines 185 and 196) with no lock.
- `Identity` (line 16) gives every test in the class the scenario `"scenario"`. Other test classes in the same project use it too, for example `KafkaNativeMonitoringBoundsTests.cs` line 17 and `KafkaNativeMonitoringCaptureTests.cs` lines 41, 94 and 126.
- `KafkaNativeMetricState.Record` runs under the lock of its own `KafkaStatisticsMetrics` instance (`KafkaNativeMetricState.cs` line 45, `KafkaStatisticsMetrics.cs` line 18). Two instances do not share a lock, so two tests record at the same time on two threads.

Confirmed by documentation (tool: Microsoft Learn MCP `microsoft_docs_search`, 2026-11-17):
- "The `InstrumentPublished` delegate is invoked for every pre-existing Instrument in the process. Newly created Instrument objects also trigger `InstrumentPublished` to be invoked." And: "Because callbacks occur on different threads, there may still be callbacks in progress after the call to `Dispose` returns." Source: [Collect metrics, "Create a custom collection tool using the .NET MeterListener API"](https://learn.microsoft.com/dotnet/core/diagnostics/metrics-collection#create-a-custom-collection-tool-using-the-net-meterlistener-api).
- "the System.Collections.Generic classes don't provide any thread synchronization; user code must provide all synchronization when items are added or removed on multiple threads concurrently." Source: [Thread-safe collections](https://learn.microsoft.com/dotnet/standard/collections/thread-safe/).
- xUnit runs test classes in parallel by default: "By default, there is a test collection per test class", and "tests in different test collections will run in parallel against each other"; "The default mode for all v2 and v3 projects is `collections`." Source: [Running tests in parallel](https://xunit.net/docs/running-tests-in-parallel), section "Parallel mode: `collections`", read 2026-11-17 through a web-fetch subagent. The repository sets no parallel mode: no `xunit.runner.json`, `testconfig.json` or `.runsettings` file exists, and no `parallel` setting appears in the test project files or `scripts/verify-dotnet.sh` (searched 2026-11-17).

Inferred: in CI, a test of another class recorded a measurement tagged `scenario=scenario` while the absent-counter test recorded its first value, and the two `List.Add` calls overwrote one slot. The CI log does not show which test ran alongside. The reproduction in 2.2 shows the mechanism produces the exact symptom; it does not prove which concurrent test caused the CI failure.

The same defect has a second failure mode. A concurrent `kafka.librdkafka.client.tx` measurement tagged `scenario=scenario` from another class would be accepted and appear as an extra value. `KafkaNativeMonitoringCaptureTests` records that instrument with that tag (lines 26 to 41). This mode has not been seen in CI; 2.2 reproduces it.

The #507 local failure on pull request #536 named no test, so this plan does not attribute it.

### 2.2 Reproduction (measured 2026-11-17, this session's Linux sandbox, 4 CPUs, .NET SDK 10.0.402, Release build)

A throwaway xUnit test, not committed, called the real `Given_AbsentCounter_When_ItReturns_Then_PreviousBaselineIsRetained` 2000 times on one thread. A background task meanwhile recorded in a loop on its own `KafkaStatisticsMetrics` and `KafkaNativeMetricState`, with the scenario tag shown. Three noise modes: `none` (no background writes), `noise` (a gauge, not `client.tx`), `noisetx` (`kafka.librdkafka.client.tx` with a rising value). `lost` counts failures whose actual value is `[3, 0]`, `[2, 0]` or `[2, 3]`; `extra` counts all other assertion failures, including `[0]` (two values lost) and longer sequences.

| Test file | Noise mode | Noise scenario tag | Failures of 2000 (lost / extra) |
|---|---|---|---|
| `main` | none | n/a | 0 / 0 |
| `main` | noise | `scenario` | 129 / 4; 152 / 9; 79 / 4; 98 / 5 (four runs) |
| `main` | noise | `other` | 0 / 0 |
| `main` | noisetx | `scenario` | 0 / 1982 |
| `ConcurrentQueue` only | noise | `scenario` | 0 / 0 |
| `ConcurrentQueue` only | noisetx | `scenario` | 0 / 1994 |
| Per-test scenario only (plain `List` kept) | none, noise, noisetx | `scenario` | 0 / 0 each |
| This plan (queue and per-test scenario) | none, noise, noisetx | `scenario` | 0 / 0 each |

What this shows:
- With no concurrent writer the test never failed, so `KafkaNativeMetricState` produced `[2, 3, 0]` every time. This falsifies a cause in the production state code.
- A concurrent writer whose tag the capture rejects causes no failure. A writer whose tag it accepts causes the exact CI symptom. This locates the cause in the capture's filter and collection.
- A thread-safe collection alone removes the lost values but still admits foreign `client.tx` values, so the per-test scenario is required.
- The per-test scenario alone removed both failure modes in the harness: the capture then stores only measurements the test records on its own thread. The plan keeps the thread-safe collection as well, because `MeterListener` callbacks can arrive on different threads (2.1) and the other listeners in the project already use one. That part is a defensive choice, not a measured need.

With this plan's change applied, `dotnet test tests/Parchwell.Kafka.Tests -c Release --no-build --filter "Category=Unit"` passed 10 runs of 10, 485 tests each. Before the change, 20 runs of the same command all passed too: the full suite reproduces the failure too rarely to show a fix, which is why section 9 uses the harness.

Revision 2 measurements, same machine, a scratch worktree of `main` at `35f0fd5f`, 2026-11-17:
- The per-test-scenario-only row of the table above.
- The test of 8.2, added to the unchanged `main` file (with `new Capture()`): fails with `Expected: [2]`, `Actual: [4, 2]`. Added to the file with this plan's change: passes. Build: 0 warnings, 0 errors.
- With the change and that test, `--filter "Category=Unit"` and `--filter-trait "Category=Unit"` both reported 486 tests, 486 succeeded. The documented xUnit option is `--filter-trait` ([Microsoft Testing Platform](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform), as cited by the plan check), so section 9 uses it.

## 3. Scope

Files allowed to change:
- `tests/Parchwell.Kafka.Tests/Statistics/KafkaNativeMetricStateTests.cs`

Must not change: every file under `src/`, every other test file, every `.csproj`, `Directory.*.props`, `scripts/`, `.github/`, `docs/`, `results/` and every `expected.yaml`. No project, package or assembly reference is added. The throwaway harness of section 9 is never committed.

No path in scope is protected (`.github/CODEOWNERS` lists `/docs/`, `/.*/`, `/*.md`, `/scripts/`, `/results/`, `*.csproj` and others, none matching the test file). The plan file itself lives on the plan branch only.

## 4. External contracts

None. The change is inside a test class. No wire format, configuration key, CLI flag, Kafka client setting, metric name, tag name or published wording changes. The metric and tag names the tests assert stay exactly as on `main`.

## 5. Requirements

1. WHEN a `KafkaNativeMetricStateTests` test creates a `Capture`, THEN the capture accepts only measurements whose `scenario` tag equals a value unique to that test instance, and no other test in the process uses that value.
2. WHEN measurements reach a `Capture` on more than one thread at once, THEN none is lost and their order of arrival is kept: the capture stores them in a `System.Collections.Concurrent.ConcurrentQueue<T>` and adds with `Enqueue`. Not `ConcurrentBag<T>` or `ConcurrentStack<T>`, which do not keep arrival order.
3. WHEN a test builds a `KafkaStatisticsIdentity` through `Identity(run)`, THEN its scenario is that test instance's unique value, and the `run`, role and client id are unchanged.
4. WHEN `Given_LiteralNativeReports_When_CountersAndGaugesTransition_Then_ValuesAndDimensionsMatch` checks the full tag set, THEN the expected `scenario` tag is the test instance's unique value; every other expected tag is unchanged.
5. WHEN another test class records on a meter named `Parchwell.Kafka.Statistics` with `scenario=scenario`, THEN no `KafkaNativeMetricStateTests` assertion sees that measurement.
6. The unique value is the prefix `scenario-` followed by `Guid.NewGuid().ToString("N")`, held in a readonly instance field. xUnit creates a new instance of the test class for each test, so each test gets its own value.

7. WHEN a second `KafkaStatisticsMetrics` records `kafka.librdkafka.client.tx` tagged `scenario=scenario` while a test's `Capture` listens, THEN that test's `Values` does not contain the foreign value. This is the deterministic form of requirement 5.

No requirement changes production code. If the implementer finds that a requirement cannot be met without a change under `src/`, the stop rule applies.

## 6. Invariants and safety rules

- No assertion is loosened, removed or skipped, and no expected value changes, except the one expected tag value of requirement 4, which follows the fixture (AGENTS.md "Code Review Rules", P1 on loosened assertions).
- No retry, no `[Fact(Skip = ...)]`, no `[Collection]` attribute and no change to xUnit parallel settings. Serialising the tests would hide the race, not remove it.
- No change under `src/`, so what #538 shipped is unchanged.
- No secrets, no Azure access, no workflow change (AGENTS.md hard safety rules 3, 7 and 8).

## 7. Standing fixture rule

When an existing test's fixture is rejected by a new check of this plan, the implementer does exactly one of:
- (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises;
- (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what this plan's checks produce.

The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it. The cold review and Codex review check each listed change. Modelled on [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.

The changes this plan itself requires are pinned in section 8.1 and are not fixture-rule changes. The prototype of section 2.2 needed no other change.

## 8. Test map

All tests are in `tests/Parchwell.Kafka.Tests/Statistics/KafkaNativeMetricStateTests.cs`, trait `Category=Unit`.

### 8.1 Changes to existing code in the test file

| Req | Place (line on `main`) | Change |
|---|---|---|
| 3, 6 | `Identity` (16) | Becomes an instance member that uses a readonly instance field holding `"scenario-" + Guid.NewGuid().ToString("N")` as the scenario. |
| 4 | `common` dictionary in the transitions test (39) | `["scenario"]` becomes that field. |
| 1 | Each `new Capture()` (21, 74, 90, 107, 136) | Passes that field. |
| 1 | `Capture` constructor (186) and filter (195) | Takes the scenario value and compares the `scenario` tag with it, not with the literal `"scenario"`. |
| 2 | `Capture._values` (185) and its add (196) | `ConcurrentQueue<(string Name, long Value, Dictionary<string, object?> Tags, string Kind)>`; the add becomes `_values.Enqueue(...)`. `Values` and `Measurements` keep their signatures and their current LINQ bodies over `_values`. |

Literal expected values stay as on `main`, including `[2, 3, 0]` (line 114), `[5]` and `[5, 2]` (lines 80 and 84), `[2, 5]` and `[2, 3, 2]` (lines 99 and 100), `[7]` (line 143), the transitions values `[2, 3]` and `[5, 1]` (lines 42 to 49), and the counts 22 and 2 (lines 55 and 65).

`Given_CounterReset_When_LaterValueRises_Then_QuarantineRemains` creates a second state with `Identity("next")` (line 82) and expects its value in the same capture (line 84). Requirement 3 gives both states the same per-test scenario, so this boundary is kept.

### 8.2 New test

`Given_ForeignMeasurementWithSharedScenario_When_Captured_Then_ItIsExcluded`, `[Fact]`, in the same class, covers requirements 1, 5 and 7. Inputs, all on the test thread, inside `using var listener = new Capture(<the test's scenario field>)`:
- the test's own `KafkaStatisticsMetrics` and a second, foreign `KafkaStatisticsMetrics`, both disposed by `using`;
- the foreign state, from the foreign metrics, with identity `new KafkaStatisticsIdentity("scenario", "foreign", KafkaClientRole.LoadGenProducer, "client")` and `compatibilityBacked: false`, records `Projection(Point(Tx, 4))` first;
- the test's own state, `Identity("own")` and `compatibilityBacked: false`, then records `Projection(Point(Tx, 2))`.

Expected: `listener.Values(Tx.Name)` equals `[2]`. Boundary: the foreign value carries the literal scenario the old filter accepted, so on `main` the same test gives `[4, 2]` (2.2). The foreign record runs first so that a filter that admits it changes position 0, not only the length.

Requirement 2, the lost-value race, has no deterministic test: it needs two threads to add at the same moment, and nothing in the suite can force that. Its regression evidence is the harness of section 9 item 2, red on `main` and green with the change. This is the finding the diagnosis skill asks to record.

## 9. Acceptance commands

1. `./scripts/verify-dotnet.sh --project Parchwell.Kafka.Tests`: exits 0, and the new test of 8.2 passes. Before the change, the new test added to the `main` file fails with `Actual: [4, 2]`; the implementer runs it once that way, then applies the change. Evidence: both outcomes and the summary line in the PR implementation notes, cited as a partial run.
2. Before-and-after harness, run twice, on `main` and on the branch. Add the throwaway file of section 9.1 to the test project, build Release, and run it once for each `A4F2_MODE` of `none`, `noise` and `noisetx`, each with 2000 iterations. Expected on `main`: `noise` shows `lost` above 0, and `noisetx` shows `extra` above 0. Expected on the branch: 0 failures in all three modes. Then delete the file and check with `git status --short` that it is gone and nothing else changed. Evidence: the six result lines in the PR implementation notes, with the SDK version and CPU count. If the `main` run shows no failure, the harness did not reproduce the cause on that machine, and the stop rule of section 16 applies.
3. On the branch, after the harness file of item 2 is deleted, run `dotnet build tests/Parchwell.Kafka.Tests -c Release`, then run `dotnet test tests/Parchwell.Kafka.Tests -c Release --no-build --filter-trait "Category=Unit"` 10 times: 10 passes. The build makes the 10 runs use the branch source without the harness, whatever order items 1 and 2 ran in. Evidence: the pass count and the test count each run reports, in the notes.
4. The verification gates of docs/agents/pull-requests.md "Verification gates", in its order, including `./scripts/verify-dotnet.sh --tier sandbox` once on the source the cold review cleared, and the hosted `.NET verification` check on the final head.
5. `npm run check:docs`: no warning in a changed file. The PR changes no documentation file.

### 9.1 Harness

The implementer writes the harness to these requirements; its exact code is not a contract. It calls the real `Given_AbsentCounter_When_ItReturns_Then_PreviousBaselineIsRetained` on a new `KafkaNativeMetricStateTests` instance 2000 times on the test thread. A background task records in a tight loop through its own `KafkaStatisticsMetrics.CreateNativeState` with scenario tag `scenario`: in mode `noise` a gauge point, in mode `noisetx` a `kafka.librdkafka.client.tx` point with a rising value, in mode `none` nothing. It counts failures as section 2.2 defines `lost` and `extra`, and writes one result line per mode to a file outside the repository. It carries its own trait so the normal filters exclude it, and every line it adds carries the marker `DEBUG-a4f2`, so `grep -rn a4f2 tests src` finds nothing before the commit.

## 10. Size estimate

Estimate, 2026-11-17, from the Revision 2 prototype: 1 file, about 36 changed lines (the prototype with the new test was 25 added and 11 removed, counted by `git diff --stat`). Size is reported, not enforced.

## 11. Deployment assumptions

None. The change is test code that runs in the test process only. It makes no network connection to Azure or Kafka, so plan rule 3's connection and load conditions do not apply. The reproduction rates in 2.2 depend on the machine's CPU count and scheduling; the plan claims no rate for CI.

## 12. Deferred work

- Other test classes that filter measurements on shared tag values. Evidence: of the other `MeterListener` uses in the test projects at the base, most store measurements in a concurrent collection or with `Interlocked` (for example `KafkaNativeMonitoringCaptureTests.cs` line 25 and `KafkaStatisticsNativeBridgeTests.cs` line 18). `ConcreteNativeFactoryCallbackTests.cs` adds to a `List` under a `lock` (lines 41 and 44). `TelemetryWiringTests.cs` assigns two local variables from its callback without synchronization (lines 36 and 37); it listens to the worker meter, not to `Parchwell.Kafka.Statistics`. Surveyed 2026-11-17. Whether their run-tag filters can collide across classes was not assessed. Not on the verdict path: these are tests, and no failure of them is reported. Responsible: a new issue, if Hari wants the audit. Gate blocked: none.

## 13. Sources

- Issue #540 body, events and comment 6958523164; #499 comment 6958521790; job 134277473583. Read with `gh api` on 2026-11-17.
- `main` at `35f0fd5f223e32942db4bfd599a69cfd9e7bacac`: `tests/Parchwell.Kafka.Tests/Statistics/KafkaNativeMetricStateTests.cs` lines 1 to 204; `src/Parchwell.Kafka/Statistics/KafkaNativeMetricState.cs` lines 1 to 171; `src/Parchwell.Kafka/Statistics/KafkaStatisticsMetrics.cs` lines 1 to 114; `KafkaNativeMonitoringBoundsTests.cs` line 17; `KafkaNativeMonitoringCaptureTests.cs` lines 26 to 41, 94 and 126; `.github/CODEOWNERS`; `scripts/verify-dotnet.sh` lines 9 to 23 and 74 to 79; `docs/engineering/dotnet.md` line 62.
- Microsoft Learn: [Collect metrics](https://learn.microsoft.com/dotnet/core/diagnostics/metrics-collection#create-a-custom-collection-tool-using-the-net-meterlistener-api) and [Thread-safe collections](https://learn.microsoft.com/dotnet/standard/collections/thread-safe/), through the Microsoft Learn MCP tool, 2026-11-17.
- xUnit: [Running tests in parallel](https://xunit.net/docs/running-tests-in-parallel), section "Parallel mode: `collections`", 2026-11-17.
- [Plan check 6958860463](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/540#issuecomment-6958860463) on Revision 1, and the xUnit [Microsoft Testing Platform](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform) page it cites for `--filter-trait` (not read by this session).
- The reproduction runs of section 2.2, in this planning session's sandbox on 2026-11-17. The harness and the prototype change were removed afterwards; `git status` showed a clean tree.
- No Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour is relied on (plan rule 1 has no case here).

## 14. Plan-check questions

(a) Effort that scales with a number unknown at planning time: none. The harness runs a fixed 2000 iterations per mode, three modes, twice; the repeated suite run is a fixed 10 runs.

(b) Stricter than a parent plan: there is no parent plan. Against the issue body, the plan adds the second failure mode (foreign values admitted) and fixes it too, because 2.2 shows the queue alone leaves it, with a deterministic test (8.2). It also requires the before-and-after harness, because 2.2 shows the full suite does not reproduce the failure often enough to show a fix.

(c) Checks that depend on code, data or behaviour that is neither on `main` nor in the pull request: none. The tests use only the test file and `src/Parchwell.Kafka` on `main`. The harness is run in the session and not committed; its results are evidence, not a check.

## 15. Open questions for Hari

None.

## 16. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 4, a requirement in section 5, the scope in section 3, or a literal expected value in section 8.1 or 8.2. It also stops if the `main` run of section 9 item 2 does not reproduce the failure, or if the branch run of that item shows any failure.
