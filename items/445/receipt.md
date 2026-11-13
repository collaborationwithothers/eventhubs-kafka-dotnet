# Authored receipt: #445

## Execution card
- Deliverable: trait value `Host` renamed to `CGroupV2` in the 7 lines of 4 files; `B10AbruptEndToEndTests` tagged `CGroupV2`; the `SessionStart` hook `.claude/hooks/start-dockerd.sh` with its offline test and registration; one doc paragraph; this receipt and the state file.
- Authorization: [plan Revision 3](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6848204985), file `items/445/plan-cgroupv2-docker-hook.md` at commit 386f36bd5b4fa5b2295e6714bbf245a9c2b6916c, approved under standing approval by [plan check 6849713128](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6849713128) (`Blocking findings: none`, open questions `None`). Recognized later decisions: [6848081896](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6848081896) (proposals 1 to 3 only), [6848259371](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6848259371) (acceptance command 10), [6849531953](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/445#issuecomment-6849531953) (revise, re-check, implement).
- Gates: plan acceptance commands 1 to 10; `npm run check:docs` before the cold review (rule 8); cold review under rule 8 (2 comprehensive, 2 focused; exhaustion: push -wip and hand off to Hari); Codex review of the head commit; merge only by Hari (protected paths `.claude/`, `scripts/`, `docs/`).
- Earliest authorized publication checkpoint: push after a cold review round with no P0 or P1 and the commands below pass; command 10 needs the pushed branch.
- Predicate map: plan section 6 (requirements 1 to 4 and 5 to 11).

## Plan check findings left to the implementer
- P2-1 (docs paragraph over-claims): applied. The two replacement sentences of check 6849713128 are in `docs/engineering/dotnet-verification.md` line 69 verbatim.
- P2-2 (receipt correction to plan section 2), literal: Correction to plan section 2: `B10AbruptOfflineImageTests` also reads `memory.events.local`, through the production capture, when `PARCHWELL_B10_OFFLINE_IMAGE` is set (`src/Parchwell.LoadGen/Run/Abrupt/B10AbruptCgroup.cs` line 50). This pull request does not tag it, because the issue's proposal 2 names only `real-kafka` callers; `Tier=Docker` keeps it out of the fast tier.
- Advisory A1 (uncapped counts in commands 6 and 9): AGENTS.md rule 11 applied. Command 6 showed 0 failures in tests without `Tier=CGroupV2` (the 2 failures are the 2 tagged tests, as plan command 6 expects), and command 9 showed 0 warnings in files this pull request changes (9 in files it does not change), so the workload was zero and no stop was needed.
- Advisory A2 (how command 10's output reaches this session, and its handoff): the implementing session starts the fresh session with a prompt that tells it to run only the two commands, change nothing, send both commands' outputs and exit statuses to the implementing session with `send_message`, and end with the one handoff comment on issue 445 that AGENTS.md GOVERNANCE "Handoffs" requires, starting `**Decision needed:** None;`. The result is recorded under "Command 10" below.

## Standing fixture rule (plan section 6)
Not applicable, as the plan states. No check that validates or rejects input is added or tightened.

## Rebase
`main` moved from 92b8128 to 696c686 during the work (the #442 verification-script change merged). The rebase was clean. The lines the plan names moved to `scripts/verify-dotnet.sh` lines 13 and 58, `scripts/test-verify-dotnet.sh` lines 130 and 131, and `docs/engineering/dotnet-verification.md` lines 58 and 66; the replacement text is the plan's.

## Commands and results
A second rebase onto `origin/main` 259a31f (the `codex/448` cleanup-script merge) was clean and changed none of this pull request's files; the SHAs of the commits changed, their content did not. Source commit of the code under test: 925bb103 before that rebase (on `origin/main` 696c686; the receipt commits after it change only `items/445/`). Commands 1 to 6 and 9 ran on it. Command 7 ran on 854f3ef8, which differs only in a blank line of `docs/engineering/dotnet-verification.md` and in `.claude/hooks/tests/start-dockerd.test.sh`, files `verify-dotnet.sh` does not read. Command 5 and 6 first ran before the rebase; the cold review found that the rebase changed `scripts/verify-dotnet.sh`, so both were re-run on 925bb103 and only those results are recorded here.

- Command 1: `grep -rn --exclude-dir=items --exclude-dir=.git --exclude-dir=node_modules -e 'Tier=Host' -e '"Tier", "Host"' .` printed nothing, exit 1.
- Command 2: `./scripts/test-verify-dotnet.sh` printed `verify-dotnet.sh checks passed.`
- Command 3: `.claude/hooks/tests/start-dockerd.test.sh` printed `start-dockerd.sh checks passed. (9 cases)`. Mutation check by hand: deleting each guard line or adding `wait` to the hook made at least one case fail.
- Command 4: `.claude/hooks/tests/require-handoff.test.sh` last line `32 passed, 0 failed`.
- Command 5: with no `dockerd` running (`pgrep -x dockerd` exit 1), `CLAUDE_CODE_REMOTE=true .claude/hooks/start-dockerd.sh </dev/null` exited 0 in 12 ms with empty stdout; `pgrep -x dockerd` then exited 0; `docker info` succeeded on the first poll; `docker version --format '{{.Server.Version}}'` printed 29.9.2. The log shows "Daemon has completed initialization" at 10:57:01 UTC.
- Command 6: `./scripts/verify-dotnet.sh --project Parchwell.LoadGen.Tests`: total 1810, failed 2, succeeded 1808, skipped 0, 8 m 50 s. By script from `results.xml`, the 2 failures are `B10AbruptEndToEndTests.Given_RealKafka_When_ParentChildAndSuccessorRunOnce_Then_ConfirmedJournalMembersAreResentWithIdenticalBytes` (traits `Tier=Docker`, `Tier=CGroupV2`; output contains `b10_abrupt_cgroup_unavailable`) and `B10AbruptLocalCgroupCaptureTests.Given_ActualLocalMembership_When_CapturedBeforeAndAfter_Then_RealIdentityAndCounterAgree` (trait `Tier=CGroupV2`; output contains `b10_abrupt_cgroup_unavailable`). So that test fails in this sandbox. No failing test lacks `Tier=CGroupV2`.
- Command 9: `npm run check:docs`: 9 warnings, all in files this pull request does not change (one blanket line in the pull request); none in `items/445/` or `docs/engineering/dotnet-verification.md`.
- Command 7: `./scripts/verify-dotnet.sh --tier fast` exited 0; last line `Partial verification passed (tier fast, projects: all). This is not the full gate`. Partial run, not the gate. It shows the `CGroupV2` tests left out of the fast tier in this sandbox.
- Command 8 (hosted `.NET verification` on the final head) and command 10 (fresh-session hook check): recorded below when taken.

## Cold review
- Round 1: mode comprehensive; input revision: this branch's index against merge base `origin/main` 696c686 (the review diff file; round 1 ran before the second rebase); specification sources: plan Revision 3 at 386f36bd5b4fa5b2295e6714bbf245a9c2b6916c, decisions 6848081896, 6848259371, 6849531953, check 6849713128 and the plan-check findings P2-1 and P2-2 above. Budget: 2 comprehensive, 2 focused. Counts so far: 1 comprehensive, 0 focused.
  Round 1 report (complete) reviewed revision 186e3daf. Findings and dispositions:
  - P1 (no source commit on commands; "the rebase changed no test or hook file" false): fixed. The receipt now names source commit 925bb103, states the rebase did change `scripts/verify-dotnet.sh`, and records commands 5 and 6 re-run on the rebased tree.
  - P2 (no blank line before the new doc paragraph): fixed.
  - P2 (false comment on `run_hook` in `start-dockerd.test.sh`): fixed.
  - P2 (race in case `start`: calls file read before the detached stub wrote): fixed; the case now polls for up to 5 s, as `dockerd_fails` does.
  - P2 (A1 disposition misstated): fixed, with the counts.
  - P2 (A2 pointed to a missing section): fixed; the A2 line now records the handling and a "Command 10" section follows.
  - P2 (state file cited a comment posted after `ready-for-agent`): fixed; the default wording is used and the comment is noted.
  - Pre-existing, not changed: CLAUDE.md "Both hooks" wording is now ambiguous with two `SessionStart` hooks. Plan section 2 forbids changing CLAUDE.md here; recorded as a follow-up candidate in the pull request.
- Round 2: mode focused; input revision: this branch at the commit after the round 1 fixes; comparison revision 51a3486a (186e3daf before the second rebase), focused diff file `focused-diff2.patch`; the full pull request diff is supplied as context. Counts after it: 1 comprehensive, 1 focused.

## Command 10
Not yet taken.
