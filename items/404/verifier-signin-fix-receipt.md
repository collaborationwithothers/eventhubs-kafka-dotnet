# Verifier sign-in fix receipt

This change gives the verifier's Kafka credential its own retry settings in every verifier mode that opens a Kafka session, so that a stalled token request is abandoned and sent again inside the verifier's initial token timeout. In the deployed pod this rests on two assumptions Hari accepted by name at 2.0 s, not on a test or a run. No check, published format, configuration key, deadline or chart value changes. Hari merges the protected verifier paths.

## Execution card

- Deliverable and scope: [verifier sign-in fix, Revision 4](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/0b9171787de9d2b285dd1ef087fa8033cea10240/items/404/plan-verifier-signin-fix.md), sections 1 to 6, 8 and 12. Pointer comment [6838148214](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6838148214).
- Authorization: standing approval under docs/agents/factory.md "Item lifecycle" step 3. [Decision 6837975192](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6837975192) adds the pull request. [Decision 6844950677](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6844950677) sets the mechanism. [Decision 6845385609](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845385609) sets the values and accepts DA-1 and DA-2 at 2.0 s. [Decision 6846551008](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846551008), paragraph "Verifier sign-in fix: option A, scope widened", applies the fix to every verifier mode that opens a Kafka session. [Plan check 6846904630](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846904630) states `Blocking findings: none`. Decisions [6845612916](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6845612916), [6846521676](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846521676) and [6846854445](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6846854445) concern other plans and do not change this one. In creation order, nothing later alters the plan's changes.
- Source: `main` at `675f837277824d197ce922511b9f55c7d77623b2`. The plan file at `0b917178` hashes to `690c90c0fd8dcb47c609e4c4eada541fddc91d0a68844052ed26143f7f0d7c02` (recomputed from `origin/claude/404-plan` by script).
- Mandatory gates: plan section 8 acceptance commands; applicable cold review; final-head CI and hosted Codex code and security reviews. No mutation target is touched. No workflow dispatch, trial, `results/`, expected outcome or verdict change.
- Commands: `./scripts/verify-dotnet.sh`; `git diff --name-only origin/main...HEAD`; the `Program.cs` block script and the worst-case script below, both with `python3 -I`; `npm run check:docs`.
- Earliest publication: after the offline gates and the applicable cold review, publish the tested pull request while CI and hosted review run together. No merge by this session.
- Fix caps: 20 verification-failure and 20 review-finding root causes, each with exhaustion `push -wip and hand off to the next session`. Cold review budget: two comprehensive passes and two focused rounds cumulatively; exhaustion hands off to Hari (docs/agents/pull-requests.md rule 8). Used so far: verification-failure root causes 2 of 20 (under Results); review-finding root causes 9 of 20 (RC1 to RC9 under Review record). Comprehensive passes 2 of 2 and focused rounds 2 of 2 are used: two comprehensive passes and the first focused round on the earlier, `b10-abrupt-verify`-only version, and the second focused round on the widened version.

## Implementation choices

- Where the code lives: `src/Parchwell.Verifier/Common/Kafka/VerifierKafkaTokenProvider.cs`, namespace `Parchwell.Verifier.Common.Kafka`, beside `VerifierKafkaSessionFactory` (plan 2.4 and docs/engineering/dotnet.md). Its `Register` method removes the shared `IKafkaTokenProvider` registration and adds this provider. `Program.cs` gets one line, `VerifierKafkaTokenProvider.Register(services);`, inside the shared Kafka block, after `AddParchwellKafkaAuthentication`. No `using` line is added to `Program.cs`. The `b10-abrupt-verify` branch is unchanged.
- Seam (R3): the provider exposes `CredentialOptions` and takes an optional transport. The transport cannot change the retry values.
- Plan check P2-A, applied (receipt text, as the check gives it): Existing verifier tests that supply an `IKafkaTokenProvider` build their own `ServiceCollection` without `Program`, and register it before `AddParchwellKafkaAuthentication`, whose `TryAddSingleton` keeps it (`VerifierOAuthComponentTests.cs` lines 37-44; `KafkaSessionTestSupport.cs` lines 18-26 at `675f8372`). Hosted component tests replace `VerifierKafkaSessionFactory` (`B10AbruptVerifyHarness.cs` lines 184-185). The new registration in `Program.cs` reaches neither. This replaces the wrong claim in plan section 2.4's fifth bullet.
- Plan check P2-B, applied (receipt text, as the check gives it): Only the initial token installation runs under `TokenTimeout` (`VerifierKafkaSession.cs` lines 39 and 61 at `675f8372`). A later refresh has no `TokenTimeout`; the per-request bound of 4.2 s applies to it as configured, not measured. The plan's section 3.3 (e) table is repeated in the worst-case script output under Results.
- Host tests: `b10-sweep-line` uses `B10SweepLineOptionsTests.Configuration` over `CaptureOptions("C1")`. `b10-sweep` and `judge` copy `SweepOptions`, `JudgeOptions` and the private `Configuration` from `B10SweepOptionsTests` unchanged. `start-line`, `verify` and `probe-gate` use `VerifierHostTestOptions.ArgumentsFor` with `--Verifier:ScenarioRoot` appended, as `VerifierHostTests.cs` does. The abrupt-mode arguments are copied from `B10AbruptVerifyHarness`. No existing test file changes.
- Test T7, an implementer choice for the reviews to check: the plan's literal input is kept (a caller token cancelled after 10 s, and cancelled 0.5 s after `GetToken` starts). On the first run of the earlier version that literal made T7 fail with POST count 0, because the 0.5 s elapsed during credential start-up before any POST was sent (verification-failure root cause 2). T7 now performs one throwaway successful acquisition with a separate provider and handler before the measured call. Expected values are unchanged. Twelve parallel runs of the earlier version's test classes, three at a time, all passed.
- The plan's standing fixture rule does not apply: no check is added or tightened and no existing test changed.

## Predicate map

The plan's section 6 is the map. Each requirement and its test, all in the two new test files:

| Requirement | Test |
|---|---|
| R1 | `VerifierKafkaTokenProviderHostTests.Given_KafkaMode_When_HostBuilt_Then_OneVerifierTokenProviderIsRegistered`, five modes (T1) |
| R3 | `...Given_KafkaMode_When_ProviderResolved_Then_CredentialCarriesPinnedRetryAndIdentity`, five modes (T2) |
| R8 | `...Given_ModeWithoutKafkaSession_When_HostBuilt_Then_NoTokenProviderIsRegistered`, `judge` and `probe-gate` (T3) |
| R2, R4 | `VerifierKafkaTokenProviderRetryTests.Given_FirstPostBlocks_When_TokenRequested_Then_OneRetrySucceeds` (T4) |
| R5 | `...Given_EveryPostBlocks_When_TokenRequested_Then_FailsAfterTwoPosts` (T5) |
| R6 | `...Given_EveryPostAnswers503_When_TokenRequested_Then_FailsAfterTwoPosts` (T6) |
| R7 | `...Given_CallerCancelsDuringFirstPost_When_TokenRequested_Then_NoSecondPostIsSent` (T7) |

T4 to T7 run the real Azure.Core 1.63.0 and MSAL 4.90.0 packages against a scripted HTTP handler, with trait `Tier` = `Slow`. R5's per-mode handling of a failed token installation is existing behaviour that this pull request does not change (plan section 6). The 0.16 s lower delay bound is not asserted, because a timer test cannot separate it from scheduling noise.

## Results

Source commit for these results: backup commit `f8098871c1b9f2c98d0eada6690e717a77485455` (the code and tests; this receipt and the state file are added after it).

- `./scripts/verify-dotnet.sh` at `f8098871`, run in the agent workspace (log kept outside the repository): `Parchwell.ArchitectureTests` 17 passed; `Parchwell.Enrichment.Tests` 56 passed; `Parchwell.Kafka.Tests` 491 passed; `Parchwell.Scenarios.Tests` 340 passed; `Parchwell.Verifier.Tests` 8271 passed, including the 16 new test cases (T1 and T2 with five each, T3 with two, T4 to T7); warning-as-error control passed. `Parchwell.LoadGen.Tests`: 1806 of 1808 passed, 2 failed (exit 2). The gate is not green here. Nothing in this receipt rests on an instruction given in the session: the two failures also occur on unchanged `main` (next item), CI at the final head decides, and Hari decides at merge whether they matter.
- Verification-failure root cause 1, two LoadGen tests that fail on unchanged `main`: `B10AbruptLocalCgroupCaptureTests.Given_ActualLocalMembership_When_CapturedBeforeAndAfter_Then_RealIdentityAndCounterAgree` and `B10AbruptEndToEndTests.Given_RealKafka_When_ParentChildAndSuccessorRunOnce_Then_ConfirmedJournalMembersAreResentWithIdenticalBytes`. Run in this workspace on a clean worktree of `origin/main` at `675f837277824d197ce922511b9f55c7d77623b2`, both failed the same way: 21 run, 19 passed, 2 failed (the end-to-end test: expected `intended_self_kill`, actual `unexpected_exit`). This pull request changes no file under `src/Parchwell.LoadGen/**` or `tests/Parchwell.LoadGen.Tests/**`. Cause, established for the first test only: it fails with `b10_abrupt_cgroup_unavailable` because `memory.events.local` is missing; this workspace uses cgroup v1 (`/proc/self/cgroup`; no `memory.events*` under `/sys/fs/cgroup`). The cause of the second test's `unexpected_exit` is not established. No test was skipped or changed. CI (`dotnet-verification.yml`, `ubuntu-24.04` line 15, triggered on pull requests line 4) is the arbiter; whether it passes them there is not known here.
- Verification-failure root cause 2, T7 timing: described under Implementation choices.
- Scope audit, `git diff --name-only origin/main...HEAD` at `f8098871` plus this receipt and the state file: `items/404/state.md`, `items/404/verifier-signin-fix-receipt.md`, `src/Parchwell.Verifier/Common/Kafka/VerifierKafkaTokenProvider.cs`, `src/Parchwell.Verifier/Program.cs`, `tests/Parchwell.Verifier.Tests/Authentication/VerifierKafkaTokenProviderRetryTests.cs`, `tests/Parchwell.Verifier.Tests/Host/VerifierKafkaTokenProviderHostTests.cs`. All are in plan section 2.2.
- `Program.cs` block script, run with `python3 -I` from the repository root:

```python
import re,subprocess
d=subprocess.run(["git","diff","-U0","origin/main","--","src/Parchwell.Verifier/Program.cs"],capture_output=True,text=True,check=True).stdout
hunks=re.findall(r"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@",d,re.M)
lines=open("src/Parchwell.Verifier/Program.cs").read().split("\n")
start=next(i for i,l in enumerate(lines,1) if l.strip()=='if (mode is "start-line" or "verify" or "b10-sweep-line" or "b10-sweep" or "b10-abrupt-verify")')
end=next(i for i in range(start+1,len(lines)+1) if lines[i-1]=="        }")
print("block lines",start,end,"hunks",hunks)
print("changed only inside block:",len(hunks)==1 and all(start<int(h[2])<=end for h in hunks))
```

Output: `block lines 151 166 hunks [('163', '0', '164', '')]` and `changed only inside block: True`. The block is the shared Kafka block; its closing line moved from 165 to 166 because of the one added line.

- Worst-case script (plan section 3.3 (e), run with `python3 -I`, extracted verbatim from the plan file at `0b917178`):

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

- `npm run check:docs`: the result at the head is recorded in the pull request description, not here, because this receipt cannot describe its own head.
- Docker: the daemon had to be started in this workspace; image pulls succeeded. The Docker-tier verifier tests passed in the gate.

## Review record

Review history of the earlier, `b10-abrupt-verify`-only version (provider under `VerifyRun/Abrupt/`, registered in the abrupt branch), against `origin/main` at `675f837277824d197ce922511b9f55c7d77623b2`. Complete reports were returned to the authoring session and are not linked.

- Round 1, comprehensive, input `cd2cc87f23020671eba711eed1c40c3ba0e07f29` plus the staged state file and receipt. Four P1 and one P2 groups, RC1 to RC5: T7 changed a pinned input; the gate was not green and a failure was attributed without evidence; the receipt lacked two script texts; the state file used words its sources do not contain; the host test copies differed from the instruction. All were fixed.
- Round 2, comprehensive, reviewed revision `0790c4bbae648b95b5e39cca2b2d1bad991a64d9`. RC6, the mandatory gate not green with no recognized authority to publish past it (P1); RC7, the state file named Codex for earlier pull requests without a source (P1); RC8, wording stronger than the sources (four P2). RC7 and RC8 were fixed. RC6 was answered `Not valid:` on the plan's stop-rule reading.
- Round 3, focused, input `9586ef17c2115f72a6effb8cffbf58408dab04ed`, comparison `0790c4bbae648b95b5e39cca2b2d1bad991a64d9`. P1: the `Not valid:` answer to RC6 is not supported by the plan text, and the gate is still not met before publication (RC6 stays open). P1: a count wording error in the review record. Everything else in the focused diff was found correct.

Disposition now: the receipt no longer answers RC6 with `Not valid:` and relies on no in-session instruction. RC6 is open and is listed for Hari in the pull request description: the plan's section 2.4 and 8 require every existing test to pass; two LoadGen tests fail in this workspace and on unchanged `main`; the final-head CI run is the evidence that decides. The count error is gone with the rewrite.

- Round 4, focused, input `13d5068c9bc6c195579e42f7b193facee4c73b1f`, comparison `9586ef17c2115f72a6effb8cffbf58408dab04ed`, on the widened version. The reviewer judged focus safe. It found the code, tests and receipt consistent with plan Revision 4 and reported three P1: RC6 still open (the mandatory gate is not green and rule 8 allows no "listed for Hari" disposition for it); the new state file line for decision 6846551008 out of creation order; and `Covers:` claiming comments whose decisions 6846521676 and 6846854445 and the open question of 6847229061 were missing from the state file (RC9, same class as RC4 and RC7).

Disposition after round 4: RC9 is corrected on the backup branch (decision lines in creation order, the two missing decisions added, the open question stated from its source). That correction has had no cold review. RC6 is not resolved: two LoadGen tests fail in this workspace and on unchanged `main`, the plan's sections 2.4, 6 and 8 require every existing test to pass, and there is no recognized decision accepting that. The cold-review budget is exhausted (2 of 2 comprehensive, 2 of 2 focused), so under docs/agents/pull-requests.md rule 8 this work is saved under rule 12 and handed to Hari. No pull request was opened and no non-backup push was made. A third comprehensive pass, or any further review round, needs his explicit decision.

Review-finding root causes used: 9 of 20 (RC1 to RC9). Verification-failure root causes used: 2 of 20.
