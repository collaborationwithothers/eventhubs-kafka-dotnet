# Plan: rename the host trait to CGroupV2, tag the abrupt-exit end-to-end test, and start Docker in Claude cloud sessions

Issue: [#445](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445). Pull request name: `cgroupv2-docker-hook`. This is the contract-grade plan for the issue's single pull request under docs/agents/factory.md "Item lifecycle" step 3. It covers the issue's proposals 1 to 3; proposal 4 is out of scope by Hari's decision.

Revision 3. Base: `main` at 92b81289b8c3fdc28883bb90bd9b56b48d2ba679. Revision 2 was written against `main` at 2077964baaa918ccf7c671b5c2370511a10cc66f, which is an ancestor of the new base. Every file this plan reads or changes is byte-identical at the two commits, so every line number and quotation below still holds; checked by comparing `git rev-parse <commit>:<path>` for each file in sections 2 and 9. The commits between them change only `items/404/`, `src/Parchwell.Verifier/` and `tests/Parchwell.Verifier.Tests/`.

Changes since Revision 2 (commit 64fc3db4b70c04e98a222b5e76c910cfe2f6fd11), all made under [decision 6849531953](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6849531953), which answers the blocking findings B1 and B2 of [plan check 6848485631](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6848485631):

- Section 1: the decision is added as authority 5.
- Section 7, D4 (B1): the sentence the check supplies is appended, stating how D4's evidence is taken.
- Section 7, D3 (B2): the description of the Docker run recorded in the issue body is replaced with the conditions that [evidence note 6849527475](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6849527475) states, which the decision adopts. The removed sentence claimed a fresh container, which the note contradicts, and named the starting tool, which no source stated before the note.
- Section 9: the evidence note is added as source 7.
- Section 7, D1 and D2: the container readings are attributed to the session that took them, the Revision 1 planning session, and the Revision 3 planning session's own repeat of them is recorded. The assumptions themselves do not change.
- The base commit above is updated, with the check that no file the plan reads or changes moved.

Nothing else changes. No gate, contract, requirement, scope, policy value, verdict rule, scientific text or safety property changes, so this revision is not design-changing under docs/agents/factory.md "Item lifecycle" step 1. Under its "Standing approval" bullet a check of this exact revision is still required.

## 1. Goal and authority

One LoadGen test needs the host's cgroup v2 memory files but carries only `Tier=Docker`. The trait value `Host` does not name the facility a test needs. Claude Code cloud sessions have `dockerd` installed but nothing starts it. This pull request renames the trait value to `CGroupV2`, adds it to the end-to-end test, and adds a `SessionStart` hook that starts `dockerd` in Claude Code cloud sessions.

Authorities, in creation order (docs/agents/factory.md "Item lifecycle" step 3):

1. The issue body (created 2026-11-13T07:50:44Z). Its section "Decision recorded in chat (Hari, 2026-11-13)" renames the trait value `Host` to `CGroupV2`. Its "Proposal" items 1 to 3 define the work. Proposal 3 names two candidates for starting `dockerd`, a `SessionStart` hook in `.claude/settings.json` or a CLAUDE.md instruction, and states "A hook is preferred: an instruction can be skipped." This plan reads that sentence, together with authority 3's "Implement proposals 1 to 3 only", as selecting the hook, and adds no CLAUDE.md instruction.
2. [Comment 6847943529](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6847943529) (2026-11-13T08:26:24Z): "Tool: Claude Code". It names the implementing tool under "Item lifecycle" step 2; it is not a decision. `ready-for-agent` was applied at 2026-11-13T08:26:07Z.
3. [Decision 6848081896](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6848081896) (2026-11-13T08:34:08Z): "Proposal 4 (measure Docker and the cgroup layout in Codex cloud tasks) is out of scope for this issue's pull request. Hari's Codex hook experiment under #443 covers it. Implement proposals 1 to 3 only." It links #443 without adopting any of #443's text, so #443 is not an authority here.
4. [Decision 6848259371](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6848259371) (2026-11-13T08:43:59Z), answering the handoff in [comment 6848218298](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6848218298) on Revision 1: "Do not accept deployment assumption D3. Add an acceptance step: before merge, a fresh Claude Code cloud session started on the pull request branch runs `pgrep -x dockerd` and `docker version --format '{{.Server.Version}}'` in its first turn. Record both outputs in the receipt. The control session may start that session." It also directs: "Revise the plan, get a fresh plan check of the revision, then implement." The handoff comment itself is evidence, not authority.
5. [Decision 6849531953](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6849531953) (2026-11-13T09:45:42Z), answering the handoff in [comment 6848494471](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6848494471) on Revision 2: "Revise Revision 2 to fix plan check findings B1 and B2 of [comment 6848485631](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6848485631). For B1, append the check's sentence to D4. For B2, describe the prior Docker run with the conditions in the [evidence note 6849527475](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6849527475). Get a fresh plan check of the revision. If it states `Blocking findings: none`, implement under standing approval." Revision 3 makes those two changes and no other change to the plan's design. The decision links and adopts the evidence note, so the note's conditions are the source for D3's prior run (source 7); the note is evidence, not authority on its own.

There is no item state file (`items/445/state.md` does not exist on `main`), no approved item plan and no parent issue.

Coordination with #442 (issue body, section "Coordination"): the current plan for #442 ([`items/442/plan-verify-run-summary.md` at c31a2aaffd502b715cc7d1ea9591761c38714c01](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/blob/c31a2aaffd502b715cc7d1ea9591761c38714c01/items/442/plan-verify-run-summary.md), line 20) states that its change edits no line that names a `Tier` value and that, if #445 merges first, it keeps #445's names. This pull request therefore has nothing to rename in #442's change. If #442 merges first, the implementer rebases onto it and re-runs acceptance command 1.

## 2. Scope

Files this pull request may change:

- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptLocalCgroupCaptureTests.cs`: the trait attribute on line 12 only.
- `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptEndToEndTests.cs`: one added attribute line after line 20 only.
- `scripts/verify-dotnet.sh`: lines 13 and 57 only.
- `scripts/test-verify-dotnet.sh`: lines 76 and 77 only.
- `docs/engineering/dotnet-verification.md`: lines 58 and 66, and one new paragraph after line 68 (section 3.4).
- `.claude/settings.json`: one added entry in the `SessionStart` hooks list (section 3.3).
- `.claude/hooks/start-dockerd.sh` (new).
- `.claude/hooks/tests/start-dockerd.test.sh` (new).
- `items/445/` (this plan, the authored receipt, and the item state file `items/445/state.md` under docs/agents/factory.md "Item state file").

Must not change: any other line of the files above; any assertion, test body or fixture in a C# test; `src/`; `.github/`; `AGENTS.md`, `CLAUDE.md`, `docs/agents/`; `.claude/hooks/handoff-session-start.sh`, `.claude/hooks/require-handoff.sh`, `.claude/hooks/handoff-lib.sh`, `.claude/hooks/tests/require-handoff.test.sh`; `results/`; `scenarios/`; any `.csproj`, `Directory.*.props` or lock file; anything under `items/` except `items/445/`.

Protected paths: `/.*/`, `/docs/`, `/scripts/` and `/*.md` are listed in `.github/CODEOWNERS` (lines 7, 5, 13 and 8 on the base), so `.claude/`, `docs/engineering/` and `scripts/` changes need Hari's merge (AGENTS.md GOVERNANCE "Merge rule").

Item 2 of the proposal, resolved by reading the code on the base: the real cgroup capture runs only when the control's mode is `real-kafka`, `RealTime` is true, `BootstrapServers` is not empty and the role is `parent` (`tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptProcessHarness.cs` lines 37 to 43). The only callers that construct a `real-kafka` fixture are `B10AbruptEndToEndTests.cs` lines 35 and 66, both in the class's single test (`[Fact]` at line 23; the class ends at line 129). The other fixture callers use modes `self-kill` (`B10AbruptProducerTests.cs` line 46), `kill`, `sleep`, `empty`/`incomplete`, `signal-failed` and `host-cross-failure` (`B10AbruptRealProcessTests.cs` lines 18, 45, 63 with the theory data at lines 59 and 60, 78 and 101), and `image-kafka` (`B10AbruptOfflineImageTests.cs` line 135). None of them reaches the capture branch. No other test needs `CGroupV2`. `B10AbruptOfflineImageTests` is not a `real-kafka` caller and returns before any work when `PARCHWELL_B10_OFFLINE_IMAGE` is unset (line 30); it is not changed.

## 3. External contracts

### 3.1 Trait values

The xUnit trait key `Tier` has the values `Docker`, `Slow` and `CGroupV2`. The value `Host` no longer appears outside `items/`.

- `B10AbruptLocalCgroupCaptureTests.cs` line 12 becomes, literal: `    [Trait("Tier", "CGroupV2")]`
- `B10AbruptEndToEndTests.cs` gets, directly after line 20 (`[Trait("Tier", "Docker")]`), the line, literal: `[Trait("Tier", "CGroupV2")]`. The class then carries both attributes, on the class, because its only test needs both facilities (docs/engineering/dotnet-verification.md line 68: "Put the trait on the class when every test in it qualifies").

### 3.2 Fast-tier filter and help text

- `scripts/verify-dotnet.sh` line 13 becomes, literal: `  --tier fast   Leave out tests marked with the xUnit trait Tier=Docker, Tier=Slow or Tier=CGroupV2.`
- `scripts/verify-dotnet.sh` line 57 becomes, literal: `    fast) tier_args=(--filter-not-trait "Tier=Docker" "Tier=Slow" "Tier=CGroupV2") ;;`
- `scripts/test-verify-dotnet.sh` lines 76 and 77 become, literal:

```
[[ $(grep -c '^TEST .*--filter-not-trait Tier=Docker Tier=Slow Tier=CGroupV2' "$STUB_LOG") -eq 6 ]] \
    || fail "every fast-tier test run must exclude Tier=Docker, Tier=Slow and Tier=CGroupV2"
```

The count 6 and the rest of the case stay as they are.

### 3.3 Hook registration

In `.claude/settings.json`, the list `.hooks.SessionStart[0].hooks` gains a second element after the existing `handoff-session-start.sh` element, literal:

```json
{
  "type": "command",
  "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/start-dockerd.sh",
  "timeout": 10
}
```

No matcher is added, so the hook runs for every `SessionStart` source. The `Stop` entry and the existing `SessionStart` element do not change.

### 3.4 Documentation wording

In `docs/engineering/dotnet-verification.md`, section "Tiers, partial runs and failure reporting":

- Line 58: the text `--filter-not-trait Tier=Docker Tier=Slow Tier=Host` becomes `--filter-not-trait Tier=Docker Tier=Slow Tier=CGroupV2`. The rest of the line does not change.
- Line 66 becomes, literal: ``| `CGroupV2` | It needs the host's cgroup v2 memory files, such as `memory.events.local`, which the abrupt-exit capture reads. Managed cloud sandboxes may not provide them. |``
- After line 68, a new paragraph, literal:

```
In a Claude Code cloud session, the `SessionStart` hook `.claude/hooks/start-dockerd.sh` starts the Docker daemon in the background, so the `Docker` tests can run there. It acts only when `CLAUDE_CODE_REMOTE` is `true`, the session runs as root, `dockerd` is installed and no `dockerd` process is running. It does not wait for the daemon. In the one measured run the daemon was ready within 40 s ([issue #445](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445)). Run `docker info` before the `Docker` tests; it fails until the daemon is ready. The daemon's output goes to `parchwell-dockerd.log` in `$TMPDIR`, or in `/tmp` when `TMPDIR` is unset. The measured sandbox had no cgroup v2 memory files, so the `CGroupV2` tests failed there. `.claude/hooks/tests/start-dockerd.test.sh` checks the hook against stub commands and starts no real daemon.
```

The 40 s figure is the issue body's measurement ("ready within 40 s", section "Measured"), so it is traceable to a cited source (AGENTS.md "Code Review Rules", numbers in documentation).

### 3.5 Hook behaviour

`.claude/hooks/start-dockerd.sh` is a Bash script with the executable bit set. Its observable behaviour:

- Exit status: always 0.
- Standard output: always empty. (A `SessionStart` hook's plain-text stdout is added as context Claude sees; source 1 in section 9.)
- It reads no input it depends on; it may leave stdin unread.
- Log file: `${TMPDIR:-/tmp}/parchwell-dockerd.log`, appended to, holding `dockerd`'s standard output and standard error.
- It starts `dockerd` with no arguments, with stdin from `/dev/null`, detached so that it keeps running after the hook exits, and returns without waiting for the daemon to become ready.

## 4. Requirements

Trait and filter:

1. WHEN `./scripts/verify-dotnet.sh --tier fast` runs THEN every `dotnet test` call receives `--filter-not-trait Tier=Docker Tier=Slow Tier=CGroupV2`, and no call receives `Tier=Host`.
2. WHEN `./scripts/verify-dotnet.sh` runs with no options THEN no test filter is passed (unchanged; `scripts/test-verify-dotnet.sh` case `default`).
3. WHEN the repository outside `items/`, `.git/` and `node_modules/` is searched THEN neither `Tier=Host` nor `"Tier", "Host"` occurs.
4. WHEN the LoadGen test report is written THEN `B10AbruptEndToEndTests`' test carries the traits `Tier=Docker` and `Tier=CGroupV2`, and `B10AbruptLocalCgroupCaptureTests`' test `Given_ActualLocalMembership_When_CapturedBeforeAndAfter_Then_RealIdentityAndCounterAgree` carries `Tier=CGroupV2` and not `Tier=Host`.

Hook, in this order of checks:

5. WHEN `CLAUDE_CODE_REMOTE` is unset or not exactly `true` THEN the hook exits 0, prints nothing and starts nothing.
6. WHEN `id -u` does not print `0` THEN the hook exits 0, prints nothing and starts nothing.
7. WHEN `command -v dockerd` finds nothing THEN the hook exits 0, prints nothing and starts nothing.
8. WHEN `command -v pgrep` finds nothing THEN the hook exits 0, prints nothing and starts nothing.
9. WHEN `pgrep -x dockerd` exits 0 THEN the hook exits 0, prints nothing and starts nothing. This also covers `resume`, `clear`, `compact` and `fork` sources after a start.
10. WHEN requirements 5 to 9 all pass THEN the hook starts `dockerd` exactly once, with no arguments, under the contract of section 3.5, and exits 0 within 5 s while `dockerd` is still running.
11. WHEN the started `dockerd` exits at once with a non-zero status THEN the hook still exits 0 and prints nothing.

## 5. Invariants and safety rules

- The full gate runs every test that ran before this pull request. No assertion, test body or fixture changes. The cgroup tests stay as they are (issue body, "Not changed").
- The hook never blocks or fails a session start. `SessionStart` cannot block (source 1, "Exit code 2 behavior per event": "`SessionStart` | No | Shows stderr to user only"); the hook also always exits 0.
- The hook writes nothing into the repository working tree.
- The hook does not change or run the handoff hooks, so the Stop hook's behaviour (CLAUDE.md "Handoffs") is unchanged.
- No workflow changes (AGENTS.md hard safety rule 8). No Azure access (hard safety rules 2 and 3). No secrets (rule 7).
- Codex cloud tasks get no change and no claim (decision 6848081896).

## 6. Test map

Rename and trait (requirements 1 to 4):

| Requirement | Test or check | Inputs | Literal expected values |
|---|---|---|---|
| 1 | `scripts/test-verify-dotnet.sh` case `fast` (lines 73 to 79, as changed in 3.2) | `--tier fast`, stub `dotnet` | exit 0; 6 `TEST` lines; 6 lines matching `^TEST .*--filter-not-trait Tier=Docker Tier=Slow Tier=CGroupV2`; `Partial verification passed` printed |
| 2 | `scripts/test-verify-dotnet.sh` case `default` (unchanged) | no options | exit 0; 6 `TEST` lines; no `filter-not-trait` |
| 3 | acceptance command 1 | the working tree | no output, grep exit status 1 |
| 4 | acceptance command 6 | `results.xml` of `Parchwell.LoadGen.Tests` | the two tests' trait elements as stated in requirement 4 |

Hook (requirements 5 to 11), all cases in the new `.claude/hooks/tests/start-dockerd.test.sh`. The test puts stub `dockerd`, `pgrep` and `id` commands on `PATH`, sets `TMPDIR` to its own temporary directory, and needs no Docker and no network. The stub `dockerd` appends one line holding its argument count and arguments to a calls file, writes `stub-dockerd-out` to stdout and `stub-dockerd-err` to stderr, then sleeps 30 s (or, for case `dockerd_fails`, exits 1 at once). The stub `id` prints the value of `FAKE_UID`. The stub `pgrep` exits 0 when `FAKE_DOCKERD_RUNNING=1` and 1 otherwise. For the cases where a command must be missing, the test runs the hook with a `PATH` that holds only a directory of links to the tools the hook needs other than the missing one. Each case runs the hook with stdin from `/dev/null` and kills any stub `dockerd` it started before the next case.

| Requirement | Case | Inputs | Literal expected values |
|---|---|---|---|
| 5 | `remote_unset` | `CLAUDE_CODE_REMOTE` unset; `FAKE_UID=0`; stubs present; not running | exit 0; stdout empty; calls file empty |
| 5 | `remote_false` | `CLAUDE_CODE_REMOTE=false`; otherwise as `start` | exit 0; stdout empty; calls file empty |
| 5 | `remote_upper` | `CLAUDE_CODE_REMOTE=TRUE`; otherwise as `start` | exit 0; stdout empty; calls file empty |
| 6 | `not_root` | `CLAUDE_CODE_REMOTE=true`; `FAKE_UID=1001` | exit 0; stdout empty; calls file empty |
| 7 | `no_dockerd` | `CLAUDE_CODE_REMOTE=true`; `FAKE_UID=0`; no `dockerd` on `PATH` | exit 0; stdout empty; no log file |
| 8 | `no_pgrep` | `CLAUDE_CODE_REMOTE=true`; `FAKE_UID=0`; no `pgrep` on `PATH` | exit 0; stdout empty; calls file empty |
| 9 | `running` | `CLAUDE_CODE_REMOTE=true`; `FAKE_UID=0`; `FAKE_DOCKERD_RUNNING=1` | exit 0; stdout empty; calls file empty |
| 10 | `start` | `CLAUDE_CODE_REMOTE=true`; `FAKE_UID=0`; not running | exit 0; stdout empty; calls file has exactly 1 line, recording 0 arguments; hook returned in under 5 s; the stub process is still alive after the hook returned; `$TMPDIR/parchwell-dockerd.log` contains `stub-dockerd-out` and `stub-dockerd-err` (polled for up to 5 s) |
| 11 | `dockerd_fails` | as `start`, stub exits 1 at once | exit 0; stdout empty; calls file has exactly 1 line |

The script prints `start-dockerd.sh checks passed.` and exits 0 when all 9 cases pass; otherwise it names each failed case and exits 1.

Boundaries: requirement 5 is checked below (unset), at a near miss in value (`false`) and at a near miss in case (`TRUE`), and at the accepted value (`start`). Requirement 6 is checked at `0` and at a non-zero uid. Requirement 10's time bound is checked against a stub that runs for 30 s, so a hook that waits would fail the 5 s bound.

Standing fixture rule (plan rule 2 of the `plan-pr` skill): not applicable. This plan adds no check that validates or rejects input, and tightens none. The changed lines of `scripts/test-verify-dotnet.sh` keep the same case, count and logic and change only the trait value the issue renames.

## 7. Deployment assumptions

Each of these concerns the Claude Code cloud container, not code in this pull request.

- D1: Claude Code cloud sessions pass `CLAUDE_CODE_REMOTE=true` to hooks. Source: the hooks reference states "The `$CLAUDE_CODE_REMOTE` environment variable is `"true"` in remote web environments and not set in the local CLI" (source 1, "Hook handler fields"). Evidence reported by the planning session of Revision 1: `env` in the Bash tool of a Claude Code cloud container showed `CLAUDE_CODE_REMOTE=true` on 2026-11-13, in a fresh container, idle. The Revision 3 planning session repeated the reading on 2026-11-13 in its own Claude Code cloud container and saw `CLAUDE_CODE_REMOTE=true`; that container was idle, with no build or test run in it by that session, and its age was not recorded. Both readings were taken in the Bash tool, not inside a hook. Requirements 5 and 10 cover both values offline.
- D2: the Claude Code cloud image has `/usr/bin/dockerd` and the session runs as root. Evidence: the issue body, section "Measured" (a Claude Code cloud sandbox, 2026-11-13, kernel 6.18.44-fc), the planning session of Revision 1 (`which dockerd` printed `/usr/bin/dockerd`, `id -u` printed `0`, `pgrep -a dockerd` found no process; fresh container, idle, as that session reported), and the Revision 3 planning session, which saw the same three results on 2026-11-13 in its own container, idle, with no build or test run in it by that session. If either changes, requirements 6 and 7 make the hook do nothing, and `Docker` tests fail visibly as they do on `main`.
- D3: a `dockerd` started detached by the hook at a real session start keeps running after the hook returns and becomes ready. The hooks reference states that "A hook can start a long-running program, such as a dev server, and leave it running while the session continues", if it runs it in the background and redirects its stdout and stderr (source 1, "Hooks that start a background process"). Readiness evidence before this pull request is the single run recorded in the issue body, section "Measured". Its conditions are stated by [evidence note 6849527475](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6849527475) from the session that took it, which [decision 6849531953](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6849531953) directs this plan to use. `dockerd` was started with `nohup dockerd > <scratch>/dockerd.log 2>&1 &` from the Bash tool of a long-running Claude Code cloud session, not from a hook. The container was not fresh: a build and a test run of the `Parchwell.LoadGen.Tests.Abrupt` namespace had finished in it at 06:45:50 UTC. No build or test was running when `dockerd` was started, so the session was idle, and no daemon was running before. Readiness was measured by polling `docker version --format '{{.Server.Version}}'` under `timeout 40`, which exited 0; the daemon log recorded "Daemon has completed initialization" at 07:29:36 UTC. The exact start time was not recorded, so the measured bound is "within 40 s". The server version was 29.9.2 and the session ran as root. The offline test (case `start`) proves the detachment against a stub only. Hari did not accept this assumption ([decision 6848259371](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6848259371)); acceptance command 10 checks it before merge, in a fresh Claude Code cloud session on the pull request branch, idle, in that session's first turn, so its evidence is taken at a real `SessionStart` with no earlier daemon. That first turn may come before the daemon is ready; the decision fixes the commands and when they run, so the plan adds no wait and no retry, and a failure for that reason is handled as any other failure of command 10.
- D4: the `CGroupV2` tests keep passing on the hosted runner. They pass there on `main` with the `Host` value; this pull request changes only the value. Evidence: hosted `.NET verification` on the final head (acceptance command 8). The evidence is taken on a fresh hosted runner job, under the load of the full gate itself.

Not relied on: whether `--filter-not-trait Tier=CGroupV2` alone excludes a test that also carries `Tier=Docker`. No command in this pull request filters on `CGroupV2` without `Docker`, and `B10AbruptEndToEndTests` is already excluded from the fast tier by `Tier=Docker`. The xUnit page describes `--filter-not-trait "name=value"` as "Do not run any test with a given trait value" (source 2); reading that as covering a test with two values for the same name is inferred. The measured fast-tier counts of `items/435/plan-verification-tiers.md` section 7, A1, show that several name=value pairs exclude a test carrying any one of them.

## 8. Acceptance commands

Run in the implementing session (a Claude Code cloud container) unless stated. The receipt records each command, its source commit and its result.

1. `grep -rn --exclude-dir=items --exclude-dir=.git --exclude-dir=node_modules -e 'Tier=Host' -e '"Tier", "Host"' .` prints nothing and exits 1. Evidence: the command and exit status. Re-run after any rebase onto a `main` that has moved.
2. `./scripts/test-verify-dotnet.sh` exits 0 and prints `verify-dotnet.sh checks passed.`
3. `.claude/hooks/tests/start-dockerd.test.sh` exits 0 and prints `start-dockerd.sh checks passed.` Evidence: output with the case count 9.
4. `.claude/hooks/tests/require-handoff.test.sh` exits 0 with `0 failed` in its last line. Evidence: its last line.
5. Live start by hand, before any Docker test: with no `dockerd` process running (`pgrep -x dockerd` exits 1), run `.claude/hooks/start-dockerd.sh </dev/null` in the session's environment, where `CLAUDE_CODE_REMOTE=true`. It exits 0 in under 5 s with empty stdout. Then `pgrep -x dockerd` exits 0, and `docker info` is polled every 5 s for up to 120 s. Evidence: the hook's exit status and duration, and the seconds until `docker info` first exited 0, measured by script under docs/agents/pull-requests.md rule 4. If `docker info` never succeeds within 120 s, record the gap and the log file's last 20 lines, and continue; commands 6 and 7 are then recorded as not run for lack of Docker.
6. With Docker ready: `./scripts/verify-dotnet.sh --project Parchwell.LoadGen.Tests`. Expected: the run fails only in `Parchwell.LoadGen.Tests tests`. Every failed test in its `results.xml` carries `Tier=CGroupV2`, and the failed tests include `B10AbruptEndToEndTests.Given_RealKafka_When_ParentChildAndSuccessorRunOnce_Then_ConfirmedJournalMembersAreResentWithIdenticalBytes`, with `b10_abrupt_cgroup_unavailable` in its output. `results.xml` shows the traits of requirement 4. Evidence: the failed test names, their traits and the reason string, extracted from `results.xml` by script. The receipt records whether `Given_ActualLocalMembership_When_CapturedBeforeAndAfter_Then_RealIdentityAndCounterAgree` passed or failed here. If a test without `Tier=CGroupV2` fails and its failure output contains `cgroup`, the implementer stops under the stop rule (the trait would need to go on another test, which changes the scope). Any other failure without `Tier=CGroupV2` is recorded in the receipt with its message, as a gap, and the implementer continues.
7. `./scripts/verify-dotnet.sh --tier fast` exits 0 and prints `Partial verification passed`. Evidence: its last line. In this sandbox it shows that the `CGroupV2` tests are left out of the fast tier.
8. Hosted `.NET verification` on the pull request's final head passes. It runs `./scripts/verify-dotnet.sh` with no options, the full gate, on a hosted runner with Docker and cgroup v2. Evidence: the run link in the pull request description under docs/agents/pull-requests.md rule 8.
9. `npm run check:docs`: no warning in `docs/engineering/dotnet-verification.md` that is not fixed or explained (docs/agents/pull-requests.md rule 8).
10. Live hook start in a fresh session ([decision 6848259371](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6848259371)). After the pull request branch is pushed and before merge, a fresh Claude Code cloud session is started on the pull request branch. The implementing session may start it. In its first turn, that session runs these two commands, in this order, before any other command, and reports each command's standard output, standard error and exit status:
    - `pgrep -x dockerd`
    - `docker version --format '{{.Server.Version}}'`

    That session changes, commits and pushes nothing. The commit it checks out has `.claude/settings.json` and `.claude/hooks/start-dockerd.sh` byte-identical to those of the final head; if a later commit changes either file, command 10 runs again on that commit. Evidence: the receipt records both commands' outputs and exit statuses and the commit SHA that session checked out. Pass: `pgrep -x dockerd` exits 0 and prints at least one process ID, and `docker version` exits 0 and prints a non-empty server version. Failure: if either command does not pass, the implementer records both outputs, does not retry in that session and does not change the hook, and hands off to Hari under the stop rule in section 12.

## 9. Sources

1. Claude Code hooks reference, https://code.claude.com/docs/en/hooks, read 2026-11-13 with the WebFetch tool. Sections "Hook handler fields" (`CLAUDE_CODE_REMOTE`; "When an event fires, its matching hooks run in parallel"), "Hooks that start a background process", "Exit code 2 behavior per event", and the statement that for `SessionStart` "Claude Code adds plain-text stdout as context that Claude can see and act on". The same page gives the matcher values `startup`, `resume`, `clear`, `compact` and `fork` for `SessionStart`.
2. xUnit.net, "Microsoft Testing Platform", https://xunit.net/docs/getting-started/v3/microsoft-testing-platform, section "Enabling the command line experience", read 2026-11-13 with the WebFetch tool: `--filter-trait "name=value"` "Run all tests with a given trait value"; `--filter-not-trait "name=value"` "Do not run any test with a given trait value". The page does not state how several values combine. The repository uses `xunit.v3.mtp-v2` 4.0.2 (`Directory.Packages.props` line 7).
3. The issue body of #445, sections "Measured", "Places that use the current name", "Proposal", "Coordination" and "Not changed".
4. Files on `main` at 2077964baaa918ccf7c671b5c2370511a10cc66f: `tests/Parchwell.LoadGen.Tests/Abrupt/B10AbruptLocalCgroupCaptureTests.cs` line 12; `B10AbruptEndToEndTests.cs` lines 20 to 24, 35, 66 and 129; `B10AbruptProcessHarness.cs` lines 37 to 43; `B10AbruptRealProcessTests.cs` lines 18, 45, 59 to 63, 78, 101 and 128 to 132; `B10AbruptProducerTests.cs` line 46; `B10AbruptOfflineImageTests.cs` lines 23, 30 and 135; `scripts/verify-dotnet.sh` lines 13, 57 and 151 to 155; `scripts/test-verify-dotnet.sh` lines 73 to 79; `docs/engineering/dotnet-verification.md` lines 58 to 68; `.claude/settings.json`; `.claude/hooks/handoff-session-start.sh`; `.github/CODEOWNERS`.
5. `items/442/plan-verify-run-summary.md` at c31a2aaffd502b715cc7d1ea9591761c38714c01, line 20.
6. `items/435/plan-verification-tiers.md` on `main`, section 7, A1.
7. [Evidence note 6849527475](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6849527475) on #445, from the session that took the run recorded in the issue body's "Measured" section, adopted by authority 5. It is the source for the conditions of that run in D3.

Documentation tools used for plan rule 1: WebFetch on the two pages above. No Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour is relied on.

## 10. Plan-check questions

(a) Steps whose effort scales with a number unknown at planning time: only the re-runs of acceptance command 10, capped below. The rename touches a fixed set of 7 lines in 4 files, listed in the issue body and confirmed on the base. Acceptance command 1 is one search; any hit outside the allowed files triggers the stop rule. Acceptance command 5 waits at most 120 s; at that cap it records the gap and continues. Acceptance command 10 runs once, and again only when a later commit changes `.claude/settings.json` or `.claude/hooks/start-dockerd.sh`; it runs at most 3 times in the pull request. At that cap: `push -wip and hand off to the next session`.

(b) Where this plan is stricter than its parent plan: there is no parent plan. Against the issue body, it adds the hook's guard conditions (requirements 5 to 9), which the issue does not state, so the hook does nothing outside Claude Code cloud sessions or where it cannot start the daemon.

(c) Tests or checks that depend on code, data or behaviour neither on `main` nor in this pull request: acceptance commands 5 and 6 depend on the Claude Code cloud container (D2, D3) and on Docker Hub images that the issue body recorded being pulled (`confluentinc/cp-kafka:7.5.12`, `testcontainers/ryuk:0.14.0`). Acceptance command 8 depends on the hosted runner (D4), checked by its recorded output on the final head. D3 was settled by neither recorded output of a hook start nor source; [decision 6848259371](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6848259371) did not accept it and added acceptance command 10, whose recorded output in a fresh Claude Code cloud session on the pull request branch checks it before merge.

## 11. Open questions for Hari

None. Revision 1's single open question, on deployment assumption D3, is answered by [decision 6848259371](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6848259371), which this revision applies as acceptance command 10.

## 12. Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change a contract in section 3, a requirement in section 4, the scope in section 2, or a literal expectation in sections 6 or 8. This includes a `Tier=Host` occurrence found outside the files in section 2, and a cgroup failure in acceptance command 6 on a test without `Tier=CGroupV2`. A failed acceptance command 10 is handed off to Hari, not to the next session: any fix changes `.claude/`, a protected path, which AGENTS.md "Before opening a pull request" rule 11 names as a reason to hand off to Hari. The handoff links the receipt entry with both outputs.
