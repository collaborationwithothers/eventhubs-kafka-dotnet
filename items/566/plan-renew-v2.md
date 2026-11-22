# Renew version 2 mutation qualifications

A mutation qualification records how long the verifier's mutation tests are expected to take and which source files support that estimate. This change lets `c5_runtime.py renew` write the version 2 assessments that record an authorized decision to keep an estimate after a shared input changes, and refuse to write anything that `check` would reject. At the base commit below, `check` rejected a correctly assessed comment change to a shared input of the committed qualification; that it rejects every such change is inferred from the code. So the command cannot yet renew that file; "Open questions for Hari" asks how to treat that.

Revision 4. One implementation pull request, `renew-v2`. Base on main: `2158a93bff0cf8b0fcd1de9ae13f776291342b19`.

## Goal and authority

The [issue body](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/566) supplies the requirements. The issue has no decision or approval comment, no parent and no approved split (catch-up of 2026-11-22: three comments, `scripts/decision-log.py --issue 566` reports none). The operation and data formats are owned by the [operations guide](../../docs/operations/verifier-mutation.md#per-estimate-timing-provenance). The operator supplies a basis: the written reason and decision reference authorizing the risk. Renewal does not measure elapsed time or approve that risk.

This plan is independent of the item planned under issue 529, which hand-writes version 2 assessments into the committed qualification. This pull request reads the committed qualification only from Git at the base commit and never writes it.

## Scope

One command change, its tests and its operator instructions form one reviewable change. Files allowed to change:

- `tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py`: the `renew` function and the helpers it calls. `check`, `validate`, `provenance_errors`, `shape` and every constant keep their current behaviour.
- `tests/Parchwell.Verifier.Tests/Mutation/test_c5_runtime.py`: new renewal tests; existing tests change only under the fixture rule below.
- `docs/operations/verifier-mutation.md`: the renewal paragraph (line 78 on the base) and the version 2 migration paragraph (line 96 on the base).

Paths that must not change include `tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json`, every other file under `tests/Parchwell.Verifier.Tests/Mutation/`, `.github/workflows/`, `scenarios/`, `results/` and `items/`. No `items/566/state.md` is written: docs/agents/factory.md "Decision log" retires state files.

Estimate on 2026-11-22: 200 to 330 added or removed lines across three files. Basis: the existing 46 nonblank lines of version 1 `renew`, the 103 nonblank lines of `test_renew_command`, five new test cases of similar size and two documentation paragraphs. Size is reported, not enforced.

## External contracts

The command is unchanged for both versions: `python3 tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py renew --head HEAD_SHA --qualification FILE --basis TEXT`. No new flag, schema field or reason code.

- Success: exit 0 and one JSON line on standard output, `{"renewed": [...], "status": "renewed"}` when the file changed, or `{"renewed": [], "status": "unchanged"}`. The list holds the sorted repository-relative paths whose hash differs between the file's previous top-level `shared_inputs` and the head's shared map. These are the existing version 1 strings (`main` prints them with `json.dumps(..., sort_keys=True)`, c5_runtime.py line 2681).
- Renewal failures: exit 1, empty standard output, one diagnostic line on standard error, and the original file left byte-identical with no new directory entry.
- Version 2 assessment shape is the existing one: `timing_inputs[<context key>].assessments.shared_inputs[<path>]` with exactly `current_sha256` (lowercase SHA-256 hex, or JSON null for a removed path) and `basis` (a nonempty array of nonblank strings). A timing context is one `timing_inputs` entry; its key is the SHA-256 of the canonical JSON of its two measured maps.
- New diagnostic texts, exact: `qualification rejected: ` followed by the sorted reason codes of the rejected result, joined by `, `.
- Existing diagnostic texts kept exact: `basis must not be empty`, `checker is not running the requested revision`, `target sources changed`, `not renewable: ` followed by sorted paths joined by `, `.
- The `check` command's output, exit codes and reason codes are unchanged.

## Requirements

1. WHEN `renew` loads either version, THEN it keeps the existing order of checks: nonblank basis, head resolution, loaded runner equal to the head's runner, JSON parse and `shape`. WHEN the file is version 1, THEN the existing version 1 behaviour is unchanged, including its refusal of every `NON_RENEWABLE` path and its `renewals` append and reversion rules.
2. WHEN the file is version 2 and the top-level target set, or any target's `source_sha256`, differs from the head, THEN it fails with `target sources changed`. Renewal never creates, edits or removes a `target_sources` assessment.
3. WHEN the file is version 2, THEN the changed paths are those of the union of the previous top-level shared paths and the head's shared paths whose hashes differ. WHEN a changed path is in `NON_RENEWABLE` (c5_runtime.py line 336: runner, both C5 workflows, campaign validator, projection helper, C5 config, tool manifest), THEN it fails with `not renewable: <sorted paths>`, except a projection helper or runner change whose previous top-level hash and head hash equal `PROJECTION_TIMING_PAIR` or `RUNNER_TIMING_PAIR` (lines 332 to 335) in that order. Reversed and other pairs are refused. Committed-tree equivalence already recorded in an assessment is kept and revalidated by requirement 5; a basis string never establishes it.
4. WHEN the file is version 2 and no refusal applies, THEN for each changed path and each timing context: if the head hash equals the context's measured hash, including both absent, it removes any assessment of that path in that context; otherwise it sets `current_sha256` to the head hash or null and sets `basis` to the earlier basis array for that path in that context with the supplied basis appended, or to `[basis]` when there was none. Assessments of unchanged paths, measured maps, context keys, `source_commit` fields, `timing_evidence`, targets, shard records and `budget_seconds` are preserved exactly. The top-level `shared_inputs` becomes the head map. No `renewals` field is written.
5. WHEN the file is version 2, THEN before reporting success, changed or unchanged, it runs the same validation as `check --head HEAD_SHA` without `--base` on the candidate. WHEN that result has status `qualified` or `provisional`, THEN it writes the candidate with one atomic replacement if it differs from the file, in the existing `json.dumps(config, indent=2, sort_keys=True) + '\n'` form, and reports success. WHEN the result is `rejected`, THEN it fails with `qualification rejected: <reasons>` and writes nothing. A missing assessment on an unrelated path is therefore rejected, not created.
6. WHEN `renew` is run twice on the same head, THEN the second run reports `unchanged` and leaves the file byte-identical, whatever basis it is given.
7. WHEN version 2 output is reviewed, THEN the reviewer, not the command, decides whether the basis names the authorizing decision and covers the changed files and the remaining uncertainty. The command enforces only nonblank text. The operations guide states this split and keeps the main-only mutation acceptance rule (docs/agents/factory.md "Evidence pack").
8. WHEN the guide is updated, THEN line 78 lists all seven `NON_RENEWABLE` paths for version 1, the renewal paragraph describes version 2 renewal under requirements 3 to 6, and line 96 no longer says `renew` supports version 1 only. The guide says that `renew` writes only a file that the full-head check accepts.

## Invariants and safety rules

- The command never edits the committed qualification; tests use temporary copies.
- No measurement, timing, estimate, evidence or verdict is created or changed. A renewal is not a measurement and does not replace mutation acceptance on `main`.
- No workflow, Azure resource or live environment is touched. AGENTS.md hard safety rules are unaffected.
- This pull request changes no C5 shared input: `input_maps` (c5_runtime.py line 467) selects `c5_workflow.py`, not `c5_runtime.py`, and selects only `.cs`, `.csproj`, `.props`, `.targets` and `.resx` files under the test project. The required C5 check on the pull request is therefore expected to report `not_applicable`; this is inferred from the code, not run.

## Test map

All fixtures except the last case are synthetic. A, B and C denote the SHA-256 hex digests of the byte strings `b'A'`, `b'B'` and `b'C'`. D1 and D2 denote the basis texts `synthetic decision 1` and `synthetic decision 2`. Tests are named cases or subtests in a new `RenewalV2Tests` class in `test_c5_runtime.py`, reusing the synthetic setup of `QualificationTests` (line 955) as `RenewalTests` does (line 2373), and invoking `runtime.main()` with patched `sys.argv` as `test_renew_command` does (line 2539).

- `v2_context_renewal` (requirements 1, 4, 5, 6). Two used timing contexts, one measured with `global.json` at A and one at B, each bound to its own shard and evidence. Previous top-level hash A. Only the B context has an assessment, `{"current_sha256": A, "basis": [D1]}`. Head C with basis D2: expect exit 0, stdout `{"renewed": ["global.json"], "status": "renewed"}`, the A context `{"current_sha256": C, "basis": [D2]}` and the B context `{"current_sha256": C, "basis": [D1, D2]}`. Then head B with basis D1: expect the B context's entry removed and the A context `{"current_sha256": B, "basis": [D2, D1]}`. A second call at head B with basis D2 returns `{"renewed": [], "status": "unchanged"}` and identical bytes. The whole result equals the input with only these changes, including one unrelated target assessment and one unrelated shared assessment kept as they were. `check` on the result reports `shared_inputs:global.json` once in `assessed_inputs`.
- `v2_membership` (requirements 4, 5). Measured and previous current `old.cs` at A, no `new.cs`. Head removes `old.cs` and adds `new.cs` at B, basis D1: expect renewed `["new.cs", "old.cs"]`, `old.cs` assessed `{"current_sha256": null, "basis": [D1]}` and `new.cs` assessed `{"current_sha256": B, "basis": [D1]}`. Then a head equal to the measured map: expect neither entry, no empty placeholder, and no `assessed_inputs` from `check` on a fixture with no other assessment.
- `v2_refusal` (requirements 1, 2, 3, 5). Each subtest expects exit 1, empty stdout, the original bytes and directory entries unchanged, and the stated stderr text. A renewable `global.json` change with `.config/dotnet-tools.json` and `.github/workflows/verifier-c5-mutation-companion.yml` changed: `not renewable: .config/dotnet-tools.json, .github/workflows/verifier-c5-mutation-companion.yml`. A changed target hash, an added target and a removed target: `target sources changed` each. Basis `'  '`: `basis must not be empty`. Loaded runner differing from the head's: `checker is not running the requested revision`. A fixture with one unrelated missing assessment, both with a renewable change and with no change: `qualification rejected: unaccounted_inputs`. An injected `os.replace` failure: stderr contains `synthetic replacement failure`.
- `v2_protected_pairs` (requirements 3, 5). Previous-to-head projection change equal to `PROJECTION_TIMING_PAIR`, and runner change equal to `RUNNER_TIMING_PAIR`, each in a fixture whose contexts are measured at the pair's first hash, with basis D1: exit 0, stdout `{"renewed": ["tests/Parchwell.Verifier.Tests/Mutation/c5_projection.py"], "status": "renewed"}` or `{"renewed": ["tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py"], "status": "renewed"}`, and in each context the assessment `{"current_sha256": <the pair's second hash>, "basis": [D1]}`. The reversed pair and a pair whose new hash differs in its last hex digit: `not renewable: tests/Parchwell.Verifier.Tests/Mutation/c5_projection.py` or `not renewable: tests/Parchwell.Verifier.Tests/Mutation/c5_workflow.py`. A newly changed `tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`, even where the existing committed-tree fixture would show it equivalent: `not renewable: tests/Parchwell.Verifier.Tests/Mutation/stryker-c5-config.json`. A context carrying an existing equivalence-backed config assessment, with `global.json` measured and previously current at A, renewed to head C with basis D1: exit 0, stdout `{"renewed": ["global.json"], "status": "renewed"}`, the config assessment byte-for-byte as before, and the `global.json` assessment `{"current_sha256": C, "basis": [D1]}`. The existing tests of the exact pairs (lines 1153, 1193 and 1240) keep passing unchanged.
- `v2_committed_rejection` (requirements 3, 4, 5, 8). Real Git, no mocks. In a temporary detached `git worktree` of `2158a93bff0cf8b0fcd1de9ae13f776291342b19`, overlay the implementation's `c5_runtime.py` at its normal path, append the line `// synthetic renewal comment` to `src/Parchwell.Verifier/AssemblyInfo.cs` and commit it with a fixed author and committer. Write `git show 2158a93bff0cf8b0fcd1de9ae13f776291342b19:tests/Parchwell.Verifier.Tests/Mutation/c5-runtime-qualification.json` to a temporary file. Run each command as a subprocess from the worktree. Expect `check --head 2158a93bff0cf8b0fcd1de9ae13f776291342b19` on that file: status `qualified`, reasons `[]`, 148 `assessed_inputs`. Expect `renew --head <synthetic commit> --basis "synthetic decision 1"`: exit 1, empty stdout, stderr `qualification rejected: invalid_estimate, invalid_renewal, missing_measurement`, file bytes unchanged. Remove the worktree and prune in a `finally` block. The synthetic commit object stays unreferenced in the object store.
- Requirement 1: existing `RenewalTests`, including `test_renew_command` and `test_committed_qualification_renews_a_comment_change`, keep their exact expectations.
- Requirements 7 and 8: document review of the command example, renewal paragraph and migration paragraph.

Existing fixture rule. When an existing test's fixture is rejected by a new check, the implementer does exactly one of: (a) changes the fixture so it passes the new checks, keeping every assertion and the boundary the test exercises; (b) where invalid input is the test's purpose, keeps the input and changes the expected values to exactly what the plan's checks produce. The rule never permits removing a case, loosening an assertion or changing a check. The PR implementation notes list each change with the check that forces it, and the cold review and Codex review check each listed change. Basis: [#404 decision 6830544567](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/404#issuecomment-6830544567) point 2.

## Deployment assumptions

None. No test depends on a deployed container, image, chart or runtime. The committed-rejection case depends on base commit history being present; the `.NET verification` checkout uses `fetch-depth: 0`, which `RequiredCheckIntegrationTests` asserts (test_c5_runtime.py line 47).

Evidence for the committed-rejection values, taken in this planning session on 2026-11-22 in the cloud container, offline and idle, no live service involved: at the base, `check --head 2158a93...` on the committed file returned `qualified`, reasons `[]`, 148 assessed inputs. A scratch script applied requirement 4's assessments for the comment change to all seven timing contexts in a temporary worktree commit and ran `check` without a base: `rejected`, reasons `["invalid_estimate", "invalid_renewal", "missing_measurement"]`. The cause traced in the code: the retained classifier-capacity route needs the head's proof tree to equal the pinned tree of `CI_REPAIR_OWNERSHIP_SOURCE` (c5_runtime.py lines 1295 to 1297, reached from `classifier_capacity_trees`, line 1313), so the runner and config equivalence of `provenance_errors` (lines 1538 to 1544) fails for shards that rely on it. The script is not committed; the implementation's test is the reproduction.

## Acceptance

From the repository root:

1. `python3 -m unittest discover -s tests/Parchwell.Verifier.Tests/Mutation -p 'test_c5_*.py'`: exit 0, with the five new cases and all existing cases passing. The committed-rejection case runs two full-head validations; each took 25 to 55 seconds in this container on 2026-11-22 (estimate for CI, based on those runs).
2. `git diff --check` and `git diff --name-only origin/main...HEAD`: no whitespace errors, and only the three files under "Scope".
3. [Verification gates](../../docs/agents/pull-requests.md#verification-gates) in order: focused tests, cold review, `./scripts/verify-dotnet.sh --tier sandbox` recorded as a partial run, and the hosted `.NET verification` check on the final head. `npm run check:docs` before the cold review. [Merge rule](../../AGENTS.md#merge-rule): the two test files match the protected pattern `Parchwell.Verifier*/` (`.github/CODEOWNERS` line 16, which mirrors the ruleset), so Hari merges. `docs/operations/` is not protected (docs/agents/factory.md "Ruleset").

## Plan-check questions

(a) Runtime-sized work: none. The test map fixes every input and transition; walking all contexts and shards is program behaviour on supplied data. An unforeseen runtime-sized step follows AGENTS.md "Before opening a pull request" rule 11.

(b) Stricter than a parent: there is no parent plan. Requirement 5 validates unchanged runs too, which version 1 does not; this enforces the issue's requirement 3 that the output passes `check`.

(c) Dependencies outside `main` and this pull request: none. The committed-rejection values rest on recorded output of the base commit's own checker, listed above.

## Sources

- [Issue requirements and acceptance](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/566).
- On the base commit, `tests/Parchwell.Verifier.Tests/Mutation/c5_runtime.py`: constants (lines 324 to 337), `input_maps` (467), `provenance_shape` (814), `classifier_ownership_matches` (1295), `classifier_capacity_trees` (1313), `provenance_errors` (1519), `shape` (1554), `validate` (1907), `check` (2061), `renew` (2115), command interface (2632).
- On the base commit, `tests/Parchwell.Verifier.Tests/Mutation/test_c5_runtime.py`: `RequiredCheckIntegrationTests` (40), `QualificationTests` (955), version 2 pair tests (1153, 1193, 1240), `RenewalTests` (2373), `test_committed_qualification_renews_a_comment_change` (2519), `test_renew_command` (2539).
- On the base commit, `docs/operations/verifier-mutation.md`: renewal paragraph (78), "Per-estimate timing provenance" (80 to 96), "Committed-tree configuration equivalence" (98).
- No Kafka, librdkafka, Confluent.Kafka or Azure Event Hubs behaviour is relied on, so no documentation tool was needed.

## Open questions for Hari

1. At the base commit, `check` rejected one correctly assessed shared-input change to the committed qualification (measured), and by the code it rejects any such change whatever its assessments (inferred), because retained timing for some shards is bound to an exact committed tree (evidence under "Deployment assumptions"). So `renew`, as the issue specifies it, cannot renew the committed file for any shared-input pull request today. The issue expected it to. Choose one:
   - (A, recommended) Proceed as planned. `renew` writes version 2 assessments and refuses, writing nothing, when `check` would reject; the committed-rejection test records today's behaviour. The command becomes useful for the committed file only once those tree-bound retentions are replaced by fresh measurement or by a separately approved checker change. Pull requests that change shared inputs keep needing your decision until then.
   - (B) `renew` writes the assessments even when `check` rejects, and reports the reasons. This contradicts the issue's requirement 3.
   - (C) Widen this item to let the tree-bound routes accept assessed shared-input differences. That changes version 2 rules adopted under issue 494 decisions and conflicts with the issue's requirement 2; it would need its own plan.

   Whether the hand-written assessments planned under issue 529 meet the same rejection was not checked.

## Stop rule

The implementer stops and hands off under AGENTS.md GOVERNANCE "Handoffs" when it needs to change an external contract, a requirement or the scope, or when the committed-rejection case produces values other than those above. That case runs the current `c5_runtime.py` against the fixed base tree, so a later correct checker change may need to update its expected values; that later change states the update and its reason.

## Changes since revision 3

Plan text only, from the [revision 3 check](https://github.com/collaborationwithothers/eventhubs-kafka-dotnet/issues/566#issuecomment-7076177073): its findings 2 and 3 applied verbatim, the "any shared-input change" claim labelled inferred (its finding 1 (iv)), and its first P2 noted under "Stop rule". None changes a gate, contract, requirement, scope, policy value or verdict rule, or adds, removes or answers an open question.

## Changes from revision 2 to revision 3

- Base moved from `ac14419f5afb6a04251e0137ca38209614482c28` to `2158a93bff0cf8b0fcd1de9ae13f776291342b19`; every line citation re-read there.
- The committed integration case now expects rejection, measured at the new base, instead of `provisional`; open question 1 added.
- `items/566/state.md` removed from scope; factory.md retired state files.
- `NON_RENEWABLE` now has seven paths (companion workflow and campaign validator added on `main`); requirement 3, the refusal test and requirement 8 cover them.
- Independence from issue 529 stated; the committed qualification is read from Git only.
- Plan branch moved to `claude/566-plan`, which starts from the revision 2 commit on `codex/566-plan`.
