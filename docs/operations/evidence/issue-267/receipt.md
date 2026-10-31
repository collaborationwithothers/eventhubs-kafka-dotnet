# C5 span shards receipt

This pull request splits the two large C5 mutation targets into parallel Stryker span jobs at member boundaries, so the workflow runs 13 jobs instead of 6. Aggregation reconciles each split target fail-closed before scoring, and shards upload Stryker logs as a separate artifact that the aggregate job does not download. It claims no mutation score, no hosted runtime and no acceptance; acceptance is a later post-merge run on `main` that Hari authorizes and dispatches.

## Execution card and authority

- Deliverable: the [C5 span shards plan, Revision 2](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/267#issuecomment-6543842230) (`updated_at` 2026-10-31T13:56:44Z), sections 1 to 6.
- Authorization: [approval 6545825882](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/267#issuecomment-6545825882) of that revision, with [decision 6544049253](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/267#issuecomment-6544049253) and [decision 6545484104](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/267#issuecomment-6545484104) applied. The approval was posted after the plan's latest edit, in light of [plan check 6545807739](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/267#issuecomment-6545807739) (`Blocking findings: none`, no `apply in implementation` finding). [Decision 6545852871](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/267#issuecomment-6545852871) names Claude Code as the implementing tool, and the issue carries `ready-for-agent`. The issue body's decisions 1 to 4 stand, including coverage off under [decision 6400987342](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/187#issuecomment-6400987342).
- Mandatory gates: local verification, plan section 6, cold review under AGENTS.md "Before opening a pull request" rule 8, final-head CI and Codex review, and Hari's protected-path merge. The workflow and the verifier tests are protected paths.
- Earliest publication checkpoint: the tested pull request after its first clean cold review.
- Review budget: AGENTS.md "Before opening a pull request" rule 8; exhaustion hands off to Hari.
- Verification commands: plan "Acceptance commands and required evidence", listed with results below.
- Caps: plan section 6 "Caps and exhaustion": at most two Stryker invocations, 2400 s each, no retry, exhaustion `record the gap and continue`.
- Base: the branch starts from `main` at `b79acb256d09ea29e493acc460ebbe2c9c59aa4f`, which planning read. During implementation, `main` moved to `2cc756be989b1f6352446c3e1c52dd47d4e6cef1`, which changes only governance files and adds docs/agents/factory.md "Item state file". This pull request follows that section: it adds `items/267/state.md`, and the implementation reads included the `ready-for-agent` label event (2026-10-31T16:08:19Z, after the tool-naming comment at 16:08:15Z). None of the files this pull request changes were changed on `main`.

## Predicate map

The plan's section 3 map, with the test that covers each predicate in `tests/Parchwell.Verifier.Tests/Mutation/test_c5_workflow.py`. Each failure test changes one field of otherwise valid evidence.

| Predicate | Below, at, above and precedence cases | Test |
| --- | --- | --- |
| Span count against the boundary limit | counts 1, 2, 3 (at limit), 4 (above); empty text with 1 and 2; counts `0`, `-1`, `True`, `"2"`, `1.0` | `SpanTests.test_span_count_below_at_and_above_boundary_limit` |
| Longest-span minimisation | tie broken low; 3 and 6 spans of `SOURCE` | `SpanTests.test_spans_minimize_the_longest_span_and_break_ties_low` |
| Boundary detection | exact `    }` lines; 5 near misses; CRLF; boundary at end of text; empty text | `SpanTests.test_member_boundaries_follow_exact_member_closing_lines` |
| Offset units | UTF-16 surrogate pair; BOM removal; invalid UTF-8 | `SpanTests.test_offsets_are_utf16_code_units_after_bom_removal` |
| Target length limit | length 91 against limits 92 (below) and 91 (at) | `SpanTests.test_target_length_limit` |
| Span configuration | every part of `PARTS`; one-part shards ignore source; span shard without source; source with too few boundaries | `SpanTests.test_span_shard_configs`, `RunnerTests.test_singleton_configs_preserve_all_other_values` |
| Committed targets split | real repeat and configuration files | `CommittedContractTests.test_committed_split_targets_have_enough_member_boundaries` |
| Reconciliation owners | 0 owners (gap), 1 owner, 2 owners (overlap, including block-filter `Ignored`) | `ReconcileTests.test_each_mutant_is_taken_from_its_one_owner`, `test_gaps_overlaps_and_inconsistent_inventories_fail` |
| Compile error and early `Ignored` precedence | identical in all parts counts once; compile error against filtered; two compile reasons | `ReconcileTests.test_identical_compile_errors_and_early_ignores_count_once`, `test_gaps_overlaps_and_inconsistent_inventories_fail` |
| Inventory and identity | extra id, different id, replacement, mutator, location; one part; empty parts | `ReconcileTests` (all four) |
| Per-target against per-shard incompleteness | one failed span blocks its target only; one-part target gets no span-shard reason; each reconciliation failure gives exactly one target reason | `AggregationTests.test_failed_span_shard_blocks_its_target`, `test_foreign_statuses_other_than_ignored_and_compile_error_are_incomplete`, `test_span_reconciliation_failures_are_incomplete` |
| Recomputed span configuration | off by one, no span, wrong part, with matching hashes | `AggregationTests.test_span_config_must_match_recomputed_member_span` |
| Elapsed time | `-0.001`, `"1"`, `True`, `None` rejected; `0` accepted | `AggregationTests.test_elapsed_time_must_be_a_nonnegative_number` |
| Rebalance threshold | 7199, 7200 not listed; 7200.001 listed; result unchanged | `AggregationTests.test_rebalance_lists_slow_shards_without_changing_the_result` |
| Rebalance span for a failed shard | timed out with readable configuration (unverified span); missing configuration; configuration without span | `AggregationTests.test_timed_out_shard_is_listed_for_rebalance`, `test_rebalance_span_of_unreadable_config_is_unknown` |
| Combined span population | 6-part target sums owned rows; per-part denominators; 13 shard entries | `AggregationTests.test_span_shards_combine_into_one_target_population` |
| Runner span selection | span computed from the projected target; selection failure launches nothing | `RunnerTests.test_span_shard_config_is_computed_from_projected_target`, `test_span_selection_failure_launches_nothing` |
| Local check timeout | `0`, `16200.5`, `True` rejected; `16200` and `5` accepted | `RunnerTests.test_local_check_rejects_invalid_input_without_launch`, `test_local_check_accepts_whole_file_and_timeout_limit` |
| Local check span | start one past a boundary, reversed, end `2147483648`, not numbers rejected; on-boundary span and `0..2147483647` accepted | same two tests, `test_local_check_runs_one_member_span_and_marks_it_diagnostic` |
| Local check other inputs | unknown target, existing output directory, dirty checkout | `RunnerTests.test_local_check_rejects_invalid_input_without_launch` |
| Artifact upload contract | both upload steps, their order, unchanged download pattern | `CommittedContractTests.test_workflow_keeps_stryker_logs_out_of_aggregate_input` |

Unreachable: a part report with no mutants reaches `reconcile` only if `report_records` accepts it, and `report_records` rejects an empty target mutant list. `reconcile([[], []])` covers the function directly.

## Pre-merge span validation (plan section 6)

Diagnostic only, not acceptance. Run in the agent's Linux workspace on commit `b291b270099d5e8e22e216d5312c684b22ae8ed0` of this branch, the code commit of this pull request, with a clean tracked checkout, SDK 10.0.402 installed in `/opt/c5-267/dotnet` by the official `dotnet-install.sh`, and `dotnet tool restore` (Stryker 5.0.0). No Azure, no workflow dispatch.

Steps 3 and 4 used the two Stryker invocations the plan allows; no step was retried. Every step met its expected result, so no design assumption is broken and the fit gate passes.

0. Artifact exclusion. `@actions/glob` 0.5.0 was installed in `/tmp/c5-267/glob` and run with `glob_check.js` on the plan's synthetic shard directory. The output equals the plan's two expected lines, byte for byte (`cmp`):

   ```text
   {"searchPaths":[""],"files":["","command.log","projection-src","projection-src/src","projection-src/src/A.cs","projection.json","receipt.json","shard-config.json","stryker","stryker/reports","stryker/reports/mutation-report.html","stryker/reports/mutation-report.json"]}
   {"searchPaths":["/stryker/logs"],"files":["stryker/logs","stryker/logs/log-1.txt","stryker/logs/test-servers","stryker/logs/test-servers/rpc-1.log"]}
   ```

1. Repeat fit. Artifact 13037984191 of [run 41183628251](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/actions/runs/41183628251) downloaded with archive digest `sha256:8c354bce572decdc97f42e3455648483204188a8140398a40f2d7f888f19b11b`, equal to the digest GitHub lists. Its report SHA-256 `9056569722f55b72f3f58ff8ffc6c51ffe5cb84f5e803b9e3a612a6a54ff22eb` equals its receipt. `SingleActionRepeatJudgement.cs` is unchanged since the measured commit `461f4a5ee2d3928aaf40a40839501b6614e40ee1`. `span_fit.py inventory ... 3 7.58` printed spans `[0, 8969]`, `[8969, 18594]`, `[18594, 2147483647]`, projected 257, 384 and 159, total 800, straddlers `[]`, as expected.
2. `member-boundaries` on the configuration target printed `31533 348`, `32521 363` and `52156 549`, as expected.
3. V1: `local-check --target VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs --span 31533..32521 --output-dir /tmp/c5-267/v1 --timeout 2400` returned 0.
4. V2: the same with `--span 52156..2147483647 --output-dir /tmp/c5-267/v2` returned 0.
5. `span_fit.py pair` on the V1 and V2 reports compared 2215 mutants and reported `"problems": []`.
6. `span_fit.py inventory <V1 report> ... 6 14.14` reported straddlers `[]` and projected 525, 347, 353, 373, 356 and 57 scheduled mutants, total 2011, equal to the 2011 the hosted run scheduled. Projected seconds are 7424, 4907, 4991, 5274, 5034 and 806; the largest is under the 10800 s gate. These are estimates, dated 2026-10-31, from the hosted rate of 14.14 s per mutant. Span 1 is projected above the 7200 s rebalance threshold, so a complete run would likely list it under `rebalance_needed`. That list is recorded only.
7. Per run, from the local receipts and command logs. Completed means lines matching `DBG] Test run for <id>:`, with no duplicate id. Local timing is recorded only; the fit uses the hosted rate.

| Run | Scheduled | Completed | CompileError | Block-filter Ignored | Mutate-filter Ignored | Killed | Survived | Elapsed seconds | Local seconds per mutant |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| V1 | 43 | 43 | 158 | 1 | 2013 | 36 | 7 | 621.2 | 14.45 |
| V2 | 57 | 57 | 158 | 5 | 1995 | 37 | 20 | 743.7 | 13.05 |

Both reports list 2215 mutants for the target, and both record 158 CompileError, equal to the hosted run. V2's 57 scheduled mutants equal step 6's projection for that span. The killed and survived counts are partial observations of two spans, not a mutation score.

| Run | Commit | Report SHA-256 | Command log SHA-256 |
| --- | --- | --- | --- |
| V1 | `b291b270099d5e8e22e216d5312c684b22ae8ed0` | `4a5020c7b3b45952ec1a1fabee576e1149f825037f85f0d496fc0bbd8111c718` | `4ebf40db0c665493a255b09756fc76151f0ba71caa27ddf6aa8b7490327bf968` |
| V2 | `b291b270099d5e8e22e216d5312c684b22ae8ed0` | `d783d4a6aaed8279b23de7db8ea9c679ca0bc4cb91054e5a5bbbe96462168587` | `eeb217c46f23803da1b069abc770fac7bd5298a628165e3b4120c45a8d4ce7bc` |

Reports, logs and local receipts stay under `/tmp/c5-267` in the agent workspace. They are not committed and are not downloadable hosted evidence.

### `glob_check.js`

```js
const glob = require('/tmp/c5-267/glob/node_modules/@actions/glob');
(async () => {
  const out = process.argv[2];
  for (const pattern of [`${out}\n!${out}/stryker/logs/**`, `${out}/stryker/logs`]) {
    const g = await glob.create(pattern, {followSymbolicLinks: true, implicitDescendants: true, omitBrokenSymbolicLinks: true, excludeHiddenFiles: true});
    const files = (await g.glob()).map(f => f.slice(out.length + 1)).sort();
    console.log(JSON.stringify({searchPaths: g.getSearchPaths().map(p => p.slice(out.length)), files}));
  }
})();
```

### `span_fit.py`

```python
"""Issue 267 diagnostic: project span-shard workload from a real Stryker report. Not acceptance."""
import json, sys
from pathlib import Path
sys.path.insert(0, "tests/Parchwell.Verifier.Tests/Mutation")
import c5_workflow as workflow

FILTERED = ("Ignored", "Removed by mutate filter")


def load(report_path, target, source_path):
    data = Path(source_path).read_bytes()
    if b"\r" in data or any(byte > 127 for byte in data):
        raise SystemExit("target is not ASCII LF text; line/column offsets need a different converter")
    text = workflow.target_text(data)
    starts = [0]
    for line in text.split("\n"):
        starts.append(starts[-1] + len(line) + 1)
    rows = workflow.report_records(json.loads(Path(report_path).read_text()), target)
    for row in rows:
        start, end = row["location"]["start"], row["location"]["end"]
        row["offsets"] = (starts[start["line"] - 1] + start["column"] - 1,
                          starts[end["line"] - 1] + end["column"] - 1)
    return text, rows


def inside(row, span):
    return span[0] <= row["offsets"][0] and row["offsets"][1] <= span[1]


def counted(row):
    disposition = (row["status"], row.get("statusReason"))
    return row["status"] != "CompileError" and (row["status"] != "Ignored" or disposition == FILTERED)


def inventory(report_path, target, source_path, parts, rate):
    text, rows = load(report_path, target, source_path)
    selected = workflow.spans(text, parts)
    result = []
    for span in selected:
        members = [row for row in rows if inside(row, span) and counted(row)]
        scheduled = [row for row in members if not (
            row["mutatorName"] == "Block removal mutation"
            and any(other is not row and inside(other, row["offsets"]) for other in members))]
        result.append({"span": list(span), "projected_scheduled": len(scheduled),
                       "projected_seconds": round(len(scheduled) * rate)})
    straddlers = [row["id"] for row in rows if not any(inside(row, span) for span in selected)]
    return {"parts": result, "straddlers": straddlers,
            "projected_total": sum(part["projected_scheduled"] for part in result)}


def pair(first_report, first_span, second_report, second_span, target, source_path):
    spans = [tuple(map(int, first_span.split(".."))), tuple(map(int, second_span.split("..")))]
    _, first = load(first_report, target, source_path)
    _, second = load(second_report, target, source_path)
    key = lambda row: (row["mutatorName"], row["replacement"], json.dumps(row["location"], sort_keys=True))
    a, b = {row["id"]: row for row in first}, {row["id"]: row for row in second}
    problems = [] if set(a) == set(b) else ["inventories differ"]
    for identifier in sorted(set(a) & set(b), key=int):
        left, right = a[identifier], b[identifier]
        dl, dr = (left["status"], left.get("statusReason")), (right["status"], right.get("statusReason"))
        if key(left) != key(right):
            problems.append(f"{identifier}: identity differs")
        elif "CompileError" in (left["status"], right["status"]):
            if dl != dr:
                problems.append(f"{identifier}: compile error differs")
        elif inside(left, spans[0]):
            if dl == FILTERED or dr != FILTERED:
                problems.append(f"{identifier}: first span ownership wrong")
        elif inside(left, spans[1]):
            if dr == FILTERED or dl != FILTERED:
                problems.append(f"{identifier}: second span ownership wrong")
        elif dl != dr or (dl != FILTERED and left["status"] != "Ignored"):
            problems.append(f"{identifier}: outside both spans but not identically excluded")
    return {"compared": len(set(a) & set(b)), "problems": problems}


if __name__ == "__main__":
    mode, *args = sys.argv[1:]
    if mode == "inventory":
        print(json.dumps(inventory(args[0], args[1], args[2], int(args[3]), float(args[4])), indent=2))
    else:
        print(json.dumps(pair(*args), indent=2))
```

## Verification

Run from the repository root on code commit `b291b270099d5e8e22e216d5312c684b22ae8ed0` (the receipt commit changes no code). Pinned lifecycle tools were installed with `bash infra/scripts/lifecycle/install-tools.sh /tmp/c5-267/tools ci`, using Python 3.12 because the pinned PyYAML wheel requires CPython 3.12. Commands ran with `PARCHWELL_TOOLS_DIRECTORY=/tmp/c5-267/tools` and that directory and its `venv/bin` first on `PATH`.

| Command | Result |
| --- | --- |
| `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_workflow.py' -v` | 62 tests, OK: the 36 existing tests, as modified, and 26 new. |
| `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py member-boundaries --target VerifyRun/SingleAction/SingleActionConfigurationJudgement.cs` | 15 lines, from `2904 32` to `53758 577`. |
| The plan's spans one-liner | repeat `[(0, 8969), (8969, 18594), (18594, 2147483647)]`; configuration `[(0, 11134), (11134, 20406), (20406, 31533), (31533, 40525), (40525, 52156), (52156, 2147483647)]`. |
| `bash infra/scripts/lifecycle/check-offline.sh` | Exit 0, including `actionlint .github/workflows/verifier-c5-mutation.yml` and the trigger check. |
| `npm ci && npm run check:docs` | Exit 0. No warning in a changed file; 8 warnings in unchanged files. |
| `git diff --check origin/main...HEAD` | No output. |
| The plan's section 4 join check | `ok`: no `{S}` remains, and the pinned Stryker base occurs 10 times in the new section. |

No .NET source changes; `dotnet-verification.yml` runs in CI.

## Review record

The cold-review reports, dispositions and cumulative counts, the final-head CI run and the hosted Codex review are recorded in the pull request description, under AGENTS.md "Before opening a pull request" rule 8.
