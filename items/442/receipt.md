# Authored receipt: #442

## Execution card
- Deliverable: UTC start and end on every stage line and in `stage-timing.tsv`, a `summary.json` run summary, the script test and the documentation of both, in `scripts/verify-dotnet.sh`, `scripts/test-verify-dotnet.sh` and `docs/engineering/dotnet-verification.md`.
- Authorization: [plan Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/442#issuecomment-6846855136) at commit c31a2aaffd502b715cc7d1ea9591761c38714c01 (`items/442/plan-verify-run-summary.md` on `claude/442-plan`), approved by [plan check 6848261407](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/442#issuecomment-6848261407) under factory.md step 3 "Standing approval", with [decision 6847884649](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/442#issuecomment-6847884649), which removed the probes and the environment-limited mode. No later decision exists.
- Gates: plan acceptance commands 1 to 4; `npm run check:docs` before the cold review (rule 8); cold review under rule 8 (2 comprehensive, 2 focused; exhaustion: push -wip and hand off to Hari); Codex review of the head commit. The change touches `/scripts/` and `/docs/`, so Hari merges.
- Earliest authorized publication checkpoint: push the branch and open the pull request after a cold review round with no P0 or P1 and after the commands under "Commands and results" pass.
- Predicate map: plan section 7 is the test map; each row has a case in `scripts/test-verify-dotnet.sh` (see "Test map to cases").

## Plan check non-blocking findings (implementer choices)
Check 6848261407 marks seven P2 findings `apply in implementation`.
- P2-1 (summary write failure): applied. Case `summary_unwritable`; the stub creates a directory at `<results>/summary.json` when `STUB_BLOCK_SUMMARY=1`. It replaces the "not covered" item of plan section 7.
- P2-2 (docs table cells): applied. The three cells of the docs table (`started_utc`/`ended_utc`, `stages`, `failed_stages`) carry the check's replacement text; the other rows are copied from plan 3.4.
- P2-3 (`jq -e` equality): applied to `failed_stages` and `slowest_tests` comparisons; entry 0 of `slowest` still builds `{name, time_s}` with `jq -c`, as the check says.
- P2-4 (`DOTNET_PROCESSOR_COUNT` check): applied. The stub appends `${DOTNET_PROCESSOR_COUNT-unset}` to `$STUB_LOG.env`; `run_case` truncates it. The `cpu` case gains the input `DOTNET_PROCESSOR_COUNT=2` (see the fixture rule below).
- P2-5 (report fixtures): applied. One `<test>` per line, `Nine` carries a `<traits>` child, fixtures are passed as absolute paths.
- P2-6 (shellcheck): `shellcheck` is not installed in this sandbox, so `shellcheck -x scripts/verify-dotnet.sh scripts/test-verify-dotnet.sh` was not run locally. The hosted `Lifecycle verification` job on the final head is an acceptance item; its result is recorded in the pull request.
- P2-7 (D2 evidence): the hosted `source_sha` is not observed. The hosted run keeps no `summary.json` artifact (plan section 9 item 3).

## Standing fixture rule (plan section 6)
Existing cases changed, with the check or harness change that forces each:
- `run_case` environment line: `env -u DOTNET_PROCESSOR_COUNT -u PARCHWELL_BUILD_MAX_CPU` (plan section 7), and `: > "$STUB_LOG.env"`.
- `unknown_project`, `unknown_tier`, `bad_cpu`: each gets its own `TMPDIR` under `$work/tmp-<case>` and an assertion that it stays empty (plan section 7). Option (a); every old assertion stays.
- `cpu`: gains `DOTNET_PROCESSOR_COUNT=2` as an environment entry, forced by plan requirement 9 and the `dotnet_processor_count` row. Option (a); every old assertion stays.
- `default`, `fast`, `select`, `fail_late` and `warning_accepted`: assertions added only.
No case removed; no assertion loosened.

## Test map to cases
All rows of plan section 7 are implemented under the case names given there: `default`, `fast`, `select`, `fail_late`, `warning_accepted`, `build_fail`, `eval_fail`, `unknown_project`, `unknown_tier`, `bad_cpu`, `help`, `slowest`, `slowest_one`, `cpu`, `cpu_text`, `no_git`, plus `summary_unwritable` from P2-1. The case count is 17 (9 on `main`, `grep -c '^run_case '`).
Uncovered, as the plan states: `mem_total_kib` `null` (the script reads the fixed path `/proc/meminfo`; the code path is the `[[ "$memory" =~ ^[0-9]+$ ]]` guard before `mem_total_kib` is set, otherwise the value stays empty and `summary_json` prints `null`).

## Commands and results (source `origin/main` 2077964, working tree of this branch)
- Acceptance 1: `./scripts/test-verify-dotnet.sh` printed `verify-dotnet.sh checks passed.`, 17 cases, about 3 s.
- Mutation check of the new assertions (local, not committed): changing `outcome=full_gate_passed`, the sort flags, the `&amp;` decoding step, the `Project evaluation` label, the `head -n 30` cap, the backslash escaping, or making the write failure path exit non-zero each made the test script report FAIL lines.
- Acceptance 2: `./scripts/verify-dotnet.sh --project Parchwell.Scenarios.Tests` in the sandbox exited 0 (partial pass line). The stage lines carry UTC stamps, for example `Locked restore: passed (6 s, 2026-11-13T08:48:13Z to 2026-11-13T08:48:19Z)`. `jq -e . summary.json` succeeded. `outcome` was `partial_passed`, `exit_code` 0, `slowest_tests` held one key with 30 entries. First entries: `ProbeV2DefinitionTests.Given_MissingDeclaration_When_Loading_Then_RequiresProbeV2(item: \"F06\")` 0.216, `SingleActionDefinitionTests.Given_MissingSingleAction_When_LoadingSelectedItem_Then_RequiresDeclaration` 0.214, `ScenarioLoaderTests.Given_InvalidProfileValues_When_Loading_Then_RejectsProfile(...)` 0.147. Total time 1 m 16 s.
- Acceptance 4: `npm run check:docs`: no warning in a changed file; 9 warnings, all in files this pull request does not change (one blanket line in the pull request).
- Acceptance 3 (hosted `.NET verification` on the final head) and the hosted `Lifecycle verification`: recorded in the pull request.
