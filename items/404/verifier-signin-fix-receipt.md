# Verifier sign-in fix receipt

This change gives the `b10-abrupt-verify` mode's Kafka credential its own retry settings, so one stalled token request no longer fails the verify Job. No check, published format, configuration key, deadline or chart value changes. Hari merges the protected verifier paths.

## Execution card

- Deliverable and scope: [verifier sign-in fix, Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/14d35b991f49ab44704b2385aca5372a99a56283/items/404/plan-verifier-signin-fix.md), sections 1 to 6, 8 and 12. Pointer comment [6838148214](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6838148214).
- Authorization: standing approval under docs/agents/factory.md "Item lifecycle" step 3. [Decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192) adds the pull request. [Decision 6844950677](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6844950677) and [decision 6845385609](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845385609) set the mechanism and values, and accept DA-1 and DA-2 at 2.0 s. [Plan check 6845555307](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845555307) states `Blocking findings: none`. Decision [6845612916](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845612916) concerns two other plans and does not change this one. In creation order, nothing later alters the plan's changes.
- Source: `main` at `675f837277824d197ce922511b9f55c7d77623b2`. The plan file at `14d35b99` hashes to `ab44dfd157543b6f1b00f4b643f83e67faf7d49bef2e4b4374589b1d233339ca` (recomputed from the branch by script).
- Mandatory gates: plan section 8 acceptance commands; full authored-diff cold review; final-head CI and hosted Codex code and security reviews. No mutation target is touched. No workflow dispatch, trial, `results/`, expected outcome or verdict change.
- Commands: `./scripts/verify-dotnet.sh`; scope audit of `git diff --name-only origin/main...HEAD`; the `Program.cs` branch script below; the plan's worst-case script with `python3 -I`; `npm run check:docs`.
- Earliest publication: after the offline gates and the cold review, publish the tested pull request while CI and hosted review run together. No merge by this session.
- Fix caps: 20 verification-failure and 20 review-finding root causes, each with exhaustion `push -wip and hand off to the next session`. Cold review budget: two comprehensive passes and two focused rounds cumulatively; exhaustion hands off to Hari (docs/agents/pull-requests.md rule 8). Used so far: verification-failure root causes 1 of 20 (environment, below); review-finding root causes 0 of 20.

## Implementation choices

- Plan check P2-A, applied: the read of the experimental `IsAzureProxyEnabled` is wrapped in `#pragma warning disable/restore AZID0002` in the new host test file only. Production code leaves the property unset.
- Plan check P2-B, applied: the sweep-mode host arguments are copied from `B10SweepOptionsTests` (`SweepOptions`, `Configuration`, private there) into the new host test file; the sweep-line mode calls the internal `B10SweepLineOptionsTests.CaptureOptions` and `Configuration`. The abrupt-mode arguments are copied from `B10AbruptVerifyHarness`. No existing test file changes.
- Plan check N2: `AbruptKafkaTokenProvider.Register` removes the shared `IKafkaTokenProvider` registration before adding its own, so one registration remains. `Program.cs` gets one line, inside the abrupt branch.
- Seam (R3): the provider exposes `CredentialOptions` and takes an optional transport. The transport cannot change the retry values.
- Test T7 trigger, an implementer choice to be checked by the reviews: the plan's input is "cancelled 0.5 s after `GetToken` starts". On the first run the timer fired during credential start-up, before any POST was sent, so the test saw POST count 0 and did not exercise "cancelled during an attempt" (R7). The test now starts the 0.5 s when the first POST arrives. Expected values are unchanged: throws, POST count 1, still 1 after a further 2 s.
- Test file layout: T1 to T3 in `tests/Parchwell.Verifier.Tests/Host/AbruptKafkaTokenProviderHostTests.cs`; T4 to T7 in `tests/Parchwell.Verifier.Tests/Authentication/AbruptKafkaTokenProviderRetryTests.cs`, both new, as plan section 2.2 allows.
- The plan's standing fixture rule does not apply: no check is added or tightened and no existing test changed.

## Predicate map

The plan's section 6 is the map. Each requirement and its test:

| Requirement | Test |
|---|---|
| R1 | `AbruptKafkaTokenProviderHostTests.Given_AbruptVerifyMode_When_HostBuilt_Then_OneAbruptTokenProviderIsRegistered` (T1) |
| R3 | `...Given_AbruptVerifyMode_When_ProviderResolved_Then_CredentialCarriesPinnedRetryAndIdentity` (T2) |
| R8 | `...Given_OtherKafkaModes_When_HostBuilt_Then_SharedTokenProviderIsKept`, four cases (T3) |
| R2, R4 | `AbruptKafkaTokenProviderRetryTests.Given_FirstPostBlocks_When_TokenRequested_Then_OneRetrySucceeds` (T4) |
| R5 | `...Given_EveryPostBlocks_When_TokenRequested_Then_FailsAfterTwoPosts` (T5) |
| R6 | `...Given_EveryPostAnswers503_When_TokenRequested_Then_FailsAfterTwoPosts` (T6) |
| R7 | `...Given_CallerCancelsDuringFirstPost_When_TokenRequested_Then_NoSecondPostIsSent` (T7) |

T4 to T7 run the real Azure.Core 1.63.0 and MSAL 4.90.0 packages against a scripted HTTP handler, with trait `Tier` = `Slow`. The unreachable cases of the plan's boundaries are not asserted: the 0.16 s lower delay bound, because a timer test cannot separate it from scheduling noise (plan section 6).

## Results

Source commit for these results: backup commit `cd2cc87f23020671eba711eed1c40c3ba0e07f29` (the five files below; this receipt is added after it).

- `./scripts/verify-dotnet.sh`, run in the agent workspace (log kept outside the repository): `Parchwell.ArchitectureTests` 17 passed; `Parchwell.Enrichment.Tests` 56 passed; `Parchwell.Kafka.Tests` 491 passed; `Parchwell.Scenarios.Tests` 340 passed; `Parchwell.Verifier.Tests` 8265 passed, including T1 to T7 (10 new test cases); warning-as-error control passed. `Parchwell.LoadGen.Tests`: 1806 of 1808 passed, 2 failed (exit 2). The gate is not green here.
- Verification-failure root cause 1, environment: both failing tests, `B10AbruptLocalCgroupCaptureTests.Given_ActualLocalMembership_When_CapturedBeforeAndAfter_Then_RealIdentityAndCounterAgree` and `B10AbruptEndToEndTests.Given_RealKafka_When_ParentChildAndSuccessorRunOnce_Then_ConfirmedJournalMembersAreResentWithIdenticalBytes`, read `memory.events.local` and fail with `b10_abrupt_cgroup_unavailable` and `FileNotFoundException`. This workspace uses cgroup v1 (`/proc/self/cgroup` lists v1 controllers; `/sys/fs/cgroup/memory.events*` does not exist). This pull request changes no file under `src/Parchwell.LoadGen/**` or `tests/Parchwell.LoadGen.Tests/**`. Inferred, not run on `main`: they fail the same way there. No test was skipped or changed; they are left to CI (`dotnet-verification.yml`, which AGENTS.md says triggers on pull requests). Whether the CI runner exposes cgroup v2 is not checked here.
- Scope audit, `git diff --name-only origin/main` (five paths, all in plan section 2.2): `items/404/state.md`, `src/Parchwell.Verifier/Program.cs`, `src/Parchwell.Verifier/VerifyRun/Abrupt/AbruptKafkaTokenProvider.cs`, `tests/Parchwell.Verifier.Tests/Authentication/AbruptKafkaTokenProviderRetryTests.cs`, `tests/Parchwell.Verifier.Tests/Host/AbruptKafkaTokenProviderHostTests.cs`; plus this receipt.
- `Program.cs` script (reads the `git diff -U0` hunks and the abrupt branch's line range): `branch lines 194 209 hunks [('195', '0', '196', '')]` and `changed only inside branch: True`.
- Worst-case script (plan section 3.3 (e), run with `python3 -I`, extracted verbatim from the plan file at `14d35b99`); output:

```
| Pinned: 2.0 s, 1, Fixed, 0.2 s | 2 | 4.2 | 8.4 | 1.6 s |
| Revision 2: 1.5 s, 1, Fixed, 0.2 s | 2 | 3.2 | 6.4 | 3.6 s |
| 1.0 s, 1, Fixed, 0.2 s | 2 | 2.2 | 4.4 | 5.6 s |
| 1.0 s, 2, Fixed, 0.2 s | 3 | 3.4 | 6.8 | 3.2 s |
| Defaults: 100 s, 3, Exponential, 0.8 s | 4 | 406.72 | 813.44 | none |
```

- `npm run check:docs`: 10 warnings, all in files this pull request does not change (`S0` identifier and link warnings in existing documents and `items/404/pr2-receipt.md`), plus one in `items/404/state.md` for this receipt's link, which resolves once this file exists.
- Docker: the daemon had to be started in this workspace; an image pull succeeded. The Docker-tier verifier tests passed in the gate.

## Review record

Round 1, mode comprehensive, being run on the full authored diff against `origin/main` at `675f837277824d197ce922511b9f55c7d77623b2`: input revision is backup commit `cd2cc87f23020671eba711eed1c40c3ba0e07f29` plus the staged `items/404/state.md` and this receipt; no comparison revision. Specification sources: the plan at `14d35b99` and the decisions listed under the execution card. Cumulative budget after this round starts: 1 of 2 comprehensive, 0 of 2 focused.
