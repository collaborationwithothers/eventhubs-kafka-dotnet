# Verifier sign-in fix receipt

This change gives the `b10-abrupt-verify` mode's Kafka credential its own retry settings, so one stalled token request no longer fails the verify Job. No check, published format, configuration key, deadline or chart value changes. Hari merges the protected verifier paths.

## Execution card

- Deliverable and scope: [verifier sign-in fix, Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/14d35b991f49ab44704b2385aca5372a99a56283/items/404/plan-verifier-signin-fix.md), sections 1 to 6, 8 and 12. Pointer comment [6838148214](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6838148214).
- Authorization: standing approval under docs/agents/factory.md "Item lifecycle" step 3. [Decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192) adds the pull request. [Decision 6844950677](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6844950677) and [decision 6845385609](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845385609) set the mechanism and values, and accept DA-1 and DA-2 at 2.0 s. [Plan check 6845555307](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845555307) states `Blocking findings: none`. Decision [6845612916](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845612916) concerns two other plans and does not change this one. In creation order, nothing later alters the plan's changes.
- Source: `main` at `675f837277824d197ce922511b9f55c7d77623b2`. The plan file at `14d35b99` hashes to `ab44dfd157543b6f1b00f4b643f83e67faf7d49bef2e4b4374589b1d233339ca` (recomputed from the branch by script).
- Mandatory gates: plan section 8 acceptance commands; full authored-diff cold review; final-head CI and hosted Codex code and security reviews. No mutation target is touched. No workflow dispatch, trial, `results/`, expected outcome or verdict change.
- Commands: `./scripts/verify-dotnet.sh`; scope audit of `git diff --name-only origin/main...HEAD`; the `Program.cs` branch script below; the plan's worst-case script with `python3 -I`; `npm run check:docs`.
- Earliest publication: after the offline gates and the cold review, publish the tested pull request while CI and hosted review run together. No merge by this session.
- Fix caps: 20 verification-failure and 20 review-finding root causes, each with exhaustion `push -wip and hand off to the next session`. Cold review budget: two comprehensive passes and two focused rounds cumulatively; exhaustion hands off to Hari (docs/agents/pull-requests.md rule 8). Used so far: verification-failure root causes 2 of 20 (the two under Results); review-finding root causes 5 of 20 (the round 1 groups under Review record).

## Implementation choices

- Plan check P2-A, applied: the read of the experimental `IsAzureProxyEnabled` is wrapped in `#pragma warning disable/restore AZID0002` in the new host test file only. Production code leaves the property unset.
- Plan check P2-B, applied: for `b10-sweep-line` the host test uses `B10SweepLineOptionsTests.Configuration` over `CaptureOptions("C1")`; for `b10-sweep` it copies `SweepOptions` and the private `Configuration` from `B10SweepOptionsTests` unchanged, including the `phase` parameter and the `ScenarioLoadOptions` loop. The abrupt-mode arguments are copied from `B10AbruptVerifyHarness`. The start-line and verify modes use `VerifierHostTestOptions.ArgumentsFor`. No existing test file changes.
- Plan check N2: `AbruptKafkaTokenProvider.Register` removes the shared `IKafkaTokenProvider` registration before adding its own, so one registration remains. `Program.cs` gets one line, inside the abrupt branch.
- Seam (R3): the provider exposes `CredentialOptions` and takes an optional transport. The transport cannot change the retry values.
- Test T7, an implementer choice for the reviews to check: the plan's literal input is kept (a caller token cancelled after 10 s, and cancelled 0.5 s after `GetToken` starts). On the first run that literal made T7 fail with POST count 0, because the 0.5 s elapsed during credential start-up before any POST was sent (verification-failure root cause 2). T7 now performs one throwaway successful acquisition with a separate provider and handler before the measured call, so assembly loading and JIT work happen outside the 0.5 s window. Expected values are unchanged. Twelve parallel runs of the new test classes, three at a time, all passed.
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

Source commit for these results: backup commit `9a4b03ee31e4936b7c7f897a8b8f57c8803a6960` for the gate and scope audit, which ran on `cd2cc87f23020671eba711eed1c40c3ba0e07f29` (the same code; `9a4b03ee` adds this receipt and the state file). The test changes made after review round 1 are re-run as stated under each item; this receipt does not describe its own head.

- `./scripts/verify-dotnet.sh`, run in the agent workspace (log kept outside the repository), before the round 1 test corrections: `Parchwell.ArchitectureTests` 17 passed; `Parchwell.Enrichment.Tests` 56 passed; `Parchwell.Kafka.Tests` 491 passed; `Parchwell.Scenarios.Tests` 340 passed; `Parchwell.Verifier.Tests` 8265 passed, including T1 to T7; warning-as-error control passed. `Parchwell.LoadGen.Tests`: 1806 of 1808 passed, 2 failed. The gate is not green here, and this pull request is published with those two failures disclosed, as the person running this session directed ("if its an environment issue then skip it and move on those are anyways run in CI"). That instruction was given in the session, not recorded on the issue.
- Verification-failure root cause 1, two LoadGen tests that fail on unchanged `main`: `B10AbruptLocalCgroupCaptureTests.Given_ActualLocalMembership_When_CapturedBeforeAndAfter_Then_RealIdentityAndCounterAgree` and `B10AbruptEndToEndTests.Given_RealKafka_When_ParentChildAndSuccessorRunOnce_Then_ConfirmedJournalMembersAreResentWithIdenticalBytes`. Run in this workspace on a clean worktree of `origin/main` at `675f837277824d197ce922511b9f55c7d77623b2`, both failed the same way: 21 run, 19 passed, 2 failed (the end-to-end test: expected `intended_self_kill`, actual `unexpected_exit`). This pull request changes no file under `src/Parchwell.LoadGen/**` or `tests/Parchwell.LoadGen.Tests/**`. Cause, established for the first test only: it fails with `b10_abrupt_cgroup_unavailable` because `memory.events.local` is missing; this workspace uses cgroup v1 (`/proc/self/cgroup`; no `memory.events*` under `/sys/fs/cgroup`). The cause of the second test's `unexpected_exit` is not established. No test was skipped or changed. CI (`dotnet-verification.yml`, `ubuntu-24.04` line 15, triggered on pull requests line 4) is the arbiter; whether it passes them there is not known here.
- Verification-failure root cause 2, T7 timing: described under Implementation choices.
- Scope audit, `git diff --name-only origin/main...HEAD` (merge-base form, as plan section 8 names it), at `9a4b03ee`: `items/404/state.md`, `items/404/verifier-signin-fix-receipt.md`, `src/Parchwell.Verifier/Program.cs`, `src/Parchwell.Verifier/VerifyRun/Abrupt/AbruptKafkaTokenProvider.cs`, `tests/Parchwell.Verifier.Tests/Authentication/AbruptKafkaTokenProviderRetryTests.cs`, `tests/Parchwell.Verifier.Tests/Host/AbruptKafkaTokenProviderHostTests.cs`. All are in plan section 2.2.
- `Program.cs` script, run with `python3 -I` from the repository root:

```python
import re,subprocess
d=subprocess.run(["git","diff","-U0","origin/main","--","src/Parchwell.Verifier/Program.cs"],capture_output=True,text=True,check=True).stdout
hunks=re.findall(r"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@",d,re.M)
lines=open("src/Parchwell.Verifier/Program.cs").read().split("\n")
start=next(i for i,l in enumerate(lines,1) if l.strip()=='if (mode == "b10-abrupt-verify")')
end=next(i for i in range(start+1,len(lines)+1) if lines[i-1]=="        }")
print("branch lines",start,end,"hunks",hunks)
ok=len(hunks)==1 and all(start<int(h[2])<=end for h in hunks)
print("changed only inside branch:",ok)
```

Output: `branch lines 194 209 hunks [('195', '0', '196', '')]` and `changed only inside branch: True`.

- Worst-case script (plan section 3.3 (e), run with `python3 -I`, extracted verbatim from the plan file at `14d35b99`):

```python
from decimal import Decimal as D
T = D("10")  # TokenTimeout seconds
rows = [
    ("Pinned: 2.0 s, 1, Fixed, 0.2 s", D("2.0"), 1, "fixed", D("0.2")),
    ("Revision 2: 1.5 s, 1, Fixed, 0.2 s", D("1.5"), 1, "fixed", D("0.2")),
    ("1.0 s, 1, Fixed, 0.2 s", D("1.0"), 1, "fixed", D("0.2")),
    ("1.0 s, 2, Fixed, 0.2 s", D("1.0"), 2, "fixed", D("0.2")),
    ("Defaults: 100 s, 3, Exponential, 0.8 s", D("100"), 3, "exp", D("0.8")),
]
J = D("1.2")  # upper jitter for Exponential; Fixed caps at Delay (factor 1.0)
for name, nt, r, mode, d in rows:
    if mode == "fixed":
        delays = d * r
    else:
        delays = sum((2 ** (n - 1)) * d * J for n in range(1, r + 1))
    per = (r + 1) * nt + delays
    later, first = per, 2 * per
    margin = T - first
    print(f"| {name} | {r+1} | {later.normalize():f} | {first.normalize():f} | " + (f"{margin.normalize():f} s |" if margin >= 0 else "none |"))
```

Output:

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

Round 1, comprehensive, input revision `cd2cc87f23020671eba711eed1c40c3ba0e07f29` plus the staged state file and receipt (same diff as backup commit `9a4b03ee31e4936b7c7f897a8b8f57c8803a6960`), against `origin/main` at `675f837277824d197ce922511b9f55c7d77623b2`; no comparison revision. Specification sources: the plan at `14d35b99` and the decisions under the execution card. The reviewer's complete report was returned to the authoring session; the review diff was frozen at SHA-256 `30cc13e63cb29b78264b03fd2064019165d6dbe74e84caaaf4ceef4e8bacd4dd`. Findings, grouped by root cause, with dispositions:

| Root cause | Findings | Disposition |
|---|---|---|
| RC1 T7 changed a pinned plan input and dropped the 10 s caller timeout | P1 | Fixed: the literal input and the 10 s caller timeout are back; a warm-up call keeps the 0.5 s window out of start-up work (Implementation choices) |
| RC2 Gate not green, and the end-to-end failure was attributed to cgroups without evidence | P1 | Fixed in the receipt: both tests were run on clean `origin/main` and fail there; only the first test's cause is claimed; publication with the disclosed failures follows the instruction quoted under Results |
| RC3 Receipt lacked the two script texts and used a non-matching scope command | P1 | Fixed: both scripts and their outputs are in Results; the scope list uses `origin/main...HEAD` |
| RC4 State file lines restated sources with words they do not contain ("authorized after the dispatch", "not part of it", "does not change this pull request") | P1 and P2 | Fixed: each line now states what its source says and nothing more |
| RC5 Host test copies of the options helpers differed from the instruction (`CaptureOptions()` and a trimmed copy) | P2 | Fixed: `CaptureOptions("C1")` and unchanged copies |

Not changed, reported by the reviewer as pre-existing: `Decisions in force` in the state file still lists PR3c-only rules; and `npm run check:docs` warnings in files this pull request does not change.

Cumulative cold-review budget: 1 of 2 comprehensive, 0 of 2 focused. Round 2 is the consolidated comprehensive re-review of the corrected diff; its record is added to the pull request's self-review record, not to this receipt.
