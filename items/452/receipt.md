# Receipt: #452 test shards

This is the single authored receipt of the pull request for issue #452. It names the approved plan, lists the implementer's choices and the fixture changes the plan's standing fixture rule allows, and records the commands run with their results. It records results for the commits it describes, not for the commit that contains it.

## Execution card

- Deliverable: the Verifier tests in three CI jobs and the LoadGen tests in two, by test class, from `scripts/dotnet-test-shards.tsv`; a method-level completeness check in the aggregate `.NET verification` job.
- Authorization: Hari's [approval 6858183518](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/452#issuecomment-6858183518) of plan r4, file `items/452/plan-test-shards.md` at commit `2226c77a98bcd937989a1062cae8c7f6e551c494` (plan comment [6856499810](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/452#issuecomment-6856499810), plan check [6858081815](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/452#issuecomment-6858081815), `Blocking findings: none`, no `apply in implementation` findings). No later decision or approval exists on the issue (read 2026-11-13). The plan file in this pull request is that commit's file, byte for byte.
- Mandatory gates and order: docs/agents/pull-requests.md "Verification gates". Step 1 is A1 and A2 below. Step 2 is the cold review. Step 3 applies. Plan r4 section 8 read the "Verification gates" text on `662685a`, where steps 1 and 3 covered a pull request that changes ".NET source or tests", and concluded step 3 does not apply. Pull request 460 (`1c2ed5e`, issue #459) then reworded that sentence: steps 1 and 3 apply to a pull request that changes any file under `src/` or `tests/`, `scripts/verify-dotnet.sh`, a `*.csproj` or `Directory.*.props` file, a `packages*.lock.json` file or `global.json`. This pull request changes `scripts/verify-dotnet.sh`, so step 3 runs once, on the source the cold review clears. docs/agents/pull-requests.md is part of AGENTS.md for conflicts, so it wins over plan section 8 on this point. The change adds a gate run; it changes no requirement of the plan. Step 4 is the hosted `.NET verification` run on the final head (A6).
- Exact verification commands: plan section 8, A1 to A8, and the pre-PR local gate `./scripts/verify-dotnet.sh --tier sandbox` (step 3).
- Predicate map: plan section 6 (S1 to S19, T1 to T47) is the map for the changed predicates of R1 to R16, with the boundary and precedence cases the plan lists. R17 and R18 are text and hash requirements checked by A3 and A7.
- Earliest authorized publication checkpoint: after the cold review has no open P0 or P1.
- Review budget: docs/agents/pull-requests.md rule 8. Exhaustion action: hand off to Hari. Hosted-run cap: plan section 11 (a), three failing A6 runs caused by this pull request and two flake reruns, exhaustion action push -wip and hand off to the next session.

## Implementer choices (factory.md "Item lifecycle" step 1)

The plan check left no `apply in implementation` finding. These are choices the plan leaves to the implementer:

1. A `<test>` element without a `type` or a `method` attribute is skipped when `check-dotnet-matrix.py` reads `results.xml` (R11). The plan defines the method string only for elements that carry both. A skipped element cannot hide a dropped listed method: the method it should have counted then has no running element, so `method-not-run` is reported. It can hide a repeated test: a skipped element in the wrong shard produces neither `class-outside-shard` nor `method-in-two-shards`. Making that case fail closed (`bad-results`) would extend R11, which the plan's stop rule reserves for Hari, so it is left as the plan has it. The case is not reachable with xUnit's writer, which puts both attributes on every `<test>`: all 313 elements of the A8 report carry both.
2. The shard-file header comment (plan section 3.2, "the implementer's") is five `#` lines that describe the format and the reason for the Verifier split.
3. `verify-dotnet.sh` matches shard-file fields with literal character sets, not ranges, so the match does not depend on the locale's collation. The checker uses `re.fullmatch`, which has no trailing-newline case.
4. R13 emits `method-in-two-shards` findings before `method-not-run` findings within a project, each in ordinal order of method. The plan gives the two rules in that order and one ordering clause for both.
5. A local real run of one shard (A8 below) uses `b10-verify-b` under `--tier sandbox`, because the plan's A8 shard needs cgroup v2, which this session lacks.

## Standing fixture rule: changes to existing tests

Under plan section 6.2. No case was removed, no assertion loosened, no check changed.

- `scripts/tests/test_check_dotnet_matrix.py`, T1 to T24, rule (a): the fixture gains `methods.json`, the `shard` key, the `test list` stage and the shard-file argument that the new checks require. `T4` expected usage line, rule (b): the usage line now reads `usage: check-dotnet-matrix.py <reports directory> <shard file> <job>=<result>...`. The subtest "only the directory" keeps its input (the directory alone, with no shard file argument) and gains the new usage line as its expected value; the subtest "directory and shard file only" is added beside it, as the plan says. `T10` also removes `methods.json`, and `T13` adds the two `shard` cases (plan T37 covers the same key); both extend the existing test.
- `scripts/test-verify-dotnet.sh`, rule (a): the stub gains a `run` case; the `default` case gains one assertion; the `help` case gains two. The existing assertions are unchanged.
- `infra/scripts/phase/tests/test_workflow.py`, rule (a): the hash and the comment line of R17.

## Results

Source: branch `claude/452-test-shards`. The commands below ran on `main` at `662685a0d54ced8fc97a438bf6fac56023fa0b90`; after round 1 the branch was rebased onto `main` at `1c2ed5e618a0a108e69ca73f4591059e68619fe2` (pull request 460, no change to a file this plan changes except `docs/engineering/dotnet-verification.md`, merged cleanly) and A1, A2 and the workflow hash test were run again with the same results. Session sandbox: .NET SDK 10.0.402, Docker running, no cgroup v2 file `memory.events.local`.

| # | Command | Result |
|---|---|---|
| A1 | `python3 -m unittest discover -s scripts/tests -p test_check_dotnet_matrix.py` | 47 tests ran, OK (T1 to T47) |
| A2 | `./scripts/test-verify-dotnet.sh` | `verify-dotnet.sh checks passed.` A mutation of the script (first shard class only; changed rejection text) made 3 checks fail; the restored script passes |
| A3 | `python3 -m unittest discover -s infra/scripts/tests -p 'test_*.py'` | unittest's summary line reports 399 tests, failures=44 and errors=103. The run printed 150 `FAIL:` and `ERROR:` header lines, because each failing subtest has its own header. Those 150 header lines are identical, by `diff`, to the header lines of a run of the same command on `main` without these changes, so they do not come from this pull request. The workflow hash test is in `infra/scripts/phase/tests/test_workflow.py`: `python3 -m unittest discover -s infra/scripts/phase/tests -p test_workflow.py` ran 25 tests, OK |
| A4 | `bash infra/scripts/lifecycle/check-offline.sh` | Not run. Its installer `install-tools.sh` needs CPython 3.12; this session has 3.13. Run instead, with the installer's pinned versions and SHA-256 values (both matched): `actionlint 1.7.12 -shellcheck=<pinned shellcheck> .github/workflows/dotnet-verification.yml` exit 0; `shellcheck 0.11.0 -x scripts/verify-dotnet.sh scripts/test-verify-dotnet.sh` exit 0. Triggers read with PyYAML: `pull_request`, `push` to `main`, `workflow_dispatch`, as `check-offline.sh` line 87 expects. The hosted `.NET verification` run repeats the checker tests |
| A5 | PyYAML comparison of `main` and the head | `on`, `permissions`, `worker-images`, `lifecycle-verification`, `terraform-verification` equal; `dotnet-verification` equal except the `run` of `Check every test project passed`; `dotnet-tests` equal after deleting the keys section 8 lists, which also leaves `Collect verification reports` `env` equal |
| A7 | `npm run check:docs` | 9 warnings, none in a file this pull request changes (the files are `docs/agents/factory.md`, `docs/operations/evidence/issue-124/README.md`, `docs/operations/evidence/issue-140/README.md`, `docs/operations/evidence/issue-211/receipt.md`, `items/404/pr2-receipt.md` and others unchanged here). Blanket line for the pull request under rule 8 |
| A8 | plan: `--project Parchwell.LoadGen.Tests --shard b10-end-to-end` | Not run: the session has no cgroup v2 file, so A6 replaces it, as the plan allows. Run instead, once, a real shard: `./scripts/verify-dotnet.sh --tier sandbox --project Parchwell.Verifier.Tests --shard b10-verify-b`: exit 0; 313 tests passed (tests stage 310 s); stage `Parchwell.Verifier.Tests test list` passed in 2 s; `methods.json` is a valid array of 1611 distinct strings; the 73 distinct `type.method` strings in `results.xml` all appear in it, and the report's types are exactly the four classes of that shard. A partial run, not the full gate |

Gate order: A1 and A2 (step 1) ran while the change moved. The cold review (step 2) follows. Step 3 applies, as the execution card says. Step 4 is A6, the hosted run on the final head; it is recorded in the pull request description, with its job durations beside plan section 4.0.

No time saving is stated here. The 310 s tests stage above is one local measurement of one shard, not a CI result.

## Cold review record

Round 1: comprehensive pass 1 of at most 2. Input: the staged tree on top of local commit `11ba353d10111c749e550be96b29124e54e78a92`, against `origin/main` at `662685a0d54ced8fc97a438bf6fac56023fa0b90`. Specification sources: the approved plan r4 (`items/452/plan-test-shards.md`, commit `2226c77a98bcd937989a1062cae8c7f6e551c494`) and Hari's approval 6858183518. No later decision exists. The report found two P1 and four P2, and no P0. Dispositions, each checked against its source before the fix:

- P1, T4 subtest "only the directory" replaced: valid (`main` has the one-argument case; the plan says T4 "gains" a subtest). Fixed: `run_checker` takes `with_shard_file`, the one-argument subtest is back with the new usage line, and "directory and shard file only" sits beside it. The fixture-rule paragraph above is corrected.
- P1, new doc paragraph cites no source for `-list`, `-noColor` and the `<type>.<method>` form: valid. Fixed: the doc cites the runner help, names the ANSI observation and marks the string form and the equality as observed, with the plan section 7 run and receipt A8.
- P2, S10 compared the file through `$(cat ...)`: valid. Fixed with `cmp` against `printf '["A.B.C"]\n'`.
- P2, "reports the project as missing": valid. Fixed to name `missing-project` and `missing-shard`.
- P2, shard file header and doc refer to each other: valid. Fixed: the doc states the validation rules and the header points to that section.
- P2, choice 1 covers only the dropped-test case: valid. Left as the plan has it, with the duplicate case stated in implementer choice 1; the alternative extends R11, which the stop rule reserves for Hari.

Round 2: comprehensive pass 2 of at most 2. Input: the branch head after the rebase onto `origin/main` at `1c2ed5e618a0a108e69ca73f4591059e68619fe2`, with this receipt as committed in that head, diffed against that `origin/main`. Comparison revision: round 1's input (`11ba353`, before the rebase and the round 1 fixes). The round 1 report was complete, and the findings above were all answered before this round. Cumulative counts after round 2 starts: comprehensive 2, focused 0.

Round 2 report: complete, no P0, one P1 and two P2, all valid against their sources and fixed:

- P1, no predicate map line in this receipt: fixed, the execution card names plan section 6 as the map.
- P2, A3 counts mixed the summary line with the header count: fixed, the A3 row now states both.
- P2, the state file's next action left out the pre-PR local gate: fixed.

Round 3: focused, input the commit that follows round 2's input `d9c2ad3951168e990ad097d81b8df5d58ba31875`, comparison `d9c2ad3951168e990ad097d81b8df5d58ba31875`. It reviews these prose corrections to `items/452/receipt.md` and `items/452/state.md` only; no script, workflow or doc source changed. Cumulative counts after round 3 starts: comprehensive 2, focused 1.
